using System.Text.Json;
using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow;

/// <summary>
/// RAG 预处理 Agent — 将用户输入转化为适合向量检索的最小查询子集。
///
/// 工作流程（两个阶段的 LLM 调用）：
///   Stage 1: 意图提取   — 让 DeepSeek 理解用户真正想要什么
///   Stage 2: 查询分解   — 将意图 + 原始问题拆解为原子化的检索子查询
///
/// 为什么需要查询分解？
///   用户的问题往往是复合的、模糊的。直接整句丢给向量数据库做相似度匹配
///   效果很差。正确做法是先拆成多个最小粒度的查询，每个查询独立检索，
///   最后将检索结果合并喂给 LLM 生成最终回复。
///
/// 示例：
///   用户输入: "对比.NET 10和Spring Boot在微服务下的性能差异"
///   →
///   意图:     技术对比（.NET 10 vs Spring Boot 微服务性能）
///   子查询:
///     [".NET 10 微服务性能基准测试",
///      "Spring Boot 微服务性能基准测试",
///      ".NET 10 Spring Boot 性能对比 benchmark",
///      ".NET 10 微服务吞吐量 启动时间",
///      "Spring Boot 微服务吞吐量 启动时间"]
///
/// 输入上下文 key：  user_input
/// 输出上下文 key：
///   rag_intent        — 提取的用户意图 (string)
///   rag_sub_queries   — 分解后的检索子查询列表 (List of string)
///   rag_query_count   — 子查询数量 (int)
///
/// 使用方式：
///   1. 直接在 Program.cs 注册为 IAgentStep，加入工作流
///   2. 也可以 new 出来后独立调用
/// </summary>
public class RAGProcessingAgent : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "RAG预处理";

    /// <summary>
    /// 子查询最小数量（确保检索覆盖度）
    /// </summary>
    private const int MinSubQueries = 2;

    /// <summary>
    /// 子查询最大数量（避免过度拆分导致噪声）
    /// </summary>
    private const int MaxSubQueries = 6;

    public RAGProcessingAgent(ILLMService llmService)
    {
        _llmService = llmService;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var userInput = context.GetValueOrDefault("user_input")?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(userInput))
        {
            context["rag_intent"] = "(空输入)";
            context["rag_sub_queries"] = new List<string>();
            context["rag_query_count"] = 0;
            return context;
        }

        // ==================================================================
        // Stage 1: 意图提取 — 让 LLM 理解用户到底要什么
        // 这一步的关键是输出一个"去歧义"的意图摘要，不给原始问题增加噪声。
        // ==================================================================
        var intent = await ExtractIntentAsync(userInput, cancellationToken);

        // ==================================================================
        // Stage 2: 查询分解 — 基于意图 + 原始问题，拆成原子化子查询
        // 每个子查询必须满足：
        //   1. 自包含 — 脱离上下文也能被理解
        //   2. 单一主题 — 一个子查询只聚焦一个检索概念
        //   3. 检索友好 — 用关键词短语而非自然语言长句
        //   4. 互补不重复 — 各子查询覆盖问题的不同维度
        // ==================================================================
        var subQueries = await DecomposeQueriesAsync(userInput, intent, cancellationToken);

        // 写入上下文，供下游 RAG 检索步骤使用
        context["rag_intent"] = intent;
        context["rag_sub_queries"] = subQueries;
        context["rag_query_count"] = subQueries.Count;

        return context;
    }

    /// <summary>
    /// Stage 1 — 意图提取。
    /// 用 low temperature 保证输出稳定、可预测。
    /// </summary>
    private async Task<string> ExtractIntentAsync(
        string userInput,
        CancellationToken ct)
    {
        var systemPrompt = """
            你是一个搜索意图分析专家。用户会提出一个问题或需求，你需要：

            1. 识别用户的核心意图（想获取知识 / 做对比 / 找方法 / 查事实 / ...）
            2. 提取关键实体和概念
            3. 判断问题复杂度（简单事实查询 / 需要多角度信息 / 高度复合）

            请用一段简洁的中文描述用户的真实信息需求（50~100 字即可）。
            注意：不要复述问题，而是提炼背后的信息需求。
            """;

        var request = new ChatRequest
        {
            Model = "deepseek-v4-flash",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userInput }
            },
            Temperature = 0.1f,   // 低温度 — 意图分析需要稳定
            MaxTokens = 256
        };

        var response = await _llmService.ChatAsync(request, ct);
        return response.Choices?.FirstOrDefault()?.Message?.Content ?? userInput;
    }

    /// <summary>
    /// Stage 2 — 查询分解。
    /// 将意图 + 原始问题拆成 {MinSubQueries}~{MaxSubQueries} 个最小检索子查询，
    /// 以 JSON 数组格式返回，方便程序解析。
    /// </summary>
    private async Task<List<string>> DecomposeQueriesAsync(
        string userInput,
        string intent,
        CancellationToken ct)
    {
        var systemPrompt = $"""
            你是一个 RAG 检索查询优化专家。给定用户的原始问题和意图分析结果，
            请将问题拆解为最小粒度的检索子查询。

            # 拆解规则：
            1. 每个子查询必须是自包含的 — 脱离上下文也能被理解
            2. 每个子查询只聚焦一个概念/实体 — 不要混合多个搜索意图
            3. 用关键词短语形式（而非自然语言长句）— 适合向量相似度检索
            4. 各子查询互补 — 合在一起覆盖用户问题的所有维度
            5. 对于对比类问题，为每个对比对象生成独立的子查询
            6. 尽可能的找最贴近的子查询，数量越少越好

            严格按以下 JSON 数组格式返回，不要加任何解释或 markdown 标记：
            ["子查询1", "子查询2", "子查询3"]
            """;

        var userContent = $"""
            - 意图分析结果：
            {intent}

            - 用户原始问题：
            {userInput}

            - 请拆解为子查询（JSON String 数组）
            """;

        var request = new ChatRequest
        {
            Model = "deepseek-v4-flash",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userContent }
            },
            Temperature = 0.01f,    // 适度低温 — 拆解需要一点创造性但不能发散
            MaxTokens = 2048
        };

        var response = await _llmService.ChatAsync(request, ct);
        var raw = response.Choices?.FirstOrDefault()?.Message?.Content ?? "[]";

        return ParseSubQueries(raw, userInput);
    }

    /// <summary>
    /// 解析 LLM 返回的子查询 JSON 数组。
    /// 做容错处理：如果 LLM 没有返回合法的 JSON 数组，退化为返回原始输入。
    /// </summary>
    private static List<string> ParseSubQueries(string raw, string fallback)
    {
        try
        {
            // 清理可能的 markdown 标记
            var cleaned = raw.Trim();
            if (cleaned.StartsWith("```"))
            {
                // 移除 ```json ... ``` 包裹
                var firstNewline = cleaned.IndexOf('\n');
                var lastBacktick = cleaned.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline > 0 && lastBacktick > firstNewline)
                    cleaned = cleaned[firstNewline..lastBacktick].Trim();
            }

            var queries = JsonSerializer.Deserialize<List<string>>(cleaned);
            if (queries != null && queries.Count > 0)
            {
                // 过滤空串和过长的子查询
                return queries
                    .Where(q => !string.IsNullOrWhiteSpace(q) && q.Length <= 200)
                    .Distinct()
                    .ToList();
            }
        }
        catch
        {
            // JSON 解析失败，退化为行拆分
            var lines = raw.Split('\n')
                .Select(l => l.Trim().TrimStart('-').TrimStart('*').Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            if (lines.Count > 0)
                return lines;
        }

        // 最终兜底：把原始输入本身作为唯一子查询
        return new List<string> { fallback };
    }
}
