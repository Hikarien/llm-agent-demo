using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Action;

/// <summary>
/// 行动类 — 联网搜索。
///
/// 对搜索引擎 API 进行检索，获取实时信息。
/// 当前为骨架实现（返回占位数据），集成真实搜索 API 时替换即可。
///
/// 集成方向：
///   - Bing Search API / Google Custom Search / SerpAPI
///   - 或使用 Tavily / Exa 等面向 AI 的搜索 API
///
/// 输入上下文 key：  "search_query"
/// 输出上下文 key：  "search_results"（JSON 字符串，包含标题+URL+摘要列表）
/// </summary>
public class WebSearchStep : IAgentStep
{
    // 如果集成真实搜索 API，在这里注入 HttpClient 和 API Key
    // private readonly HttpClient _httpClient;
    // private readonly string _apiKey;

    public string Name => "联网搜索";

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var query = context.GetValueOrDefault("search_query")?.ToString()
                 ?? context.GetValueOrDefault("user_input")?.ToString()
                 ?? "";

        // ==================================================================
        // 骨架占位 — 实际使用时替换为真实搜索 API 调用。
        //
        // 示例集成代码（以 SerpAPI 为例）：
        //   var url = $"https://serpapi.com/search?q={Uri.EscapeDataString(query)}&api_key={_apiKey}";
        //   var json = await _httpClient.GetStringAsync(url, cancellationToken);
        //   context["search_results"] = json;
        // ==================================================================

        context["search_results"] =
            $"[{{\"title\": \"[骨架占位] 搜索结果: {query}\", \"url\": \"https://example.com\", \"snippet\": \"搜索骨架输出\"}}]";

        // 模拟网络延迟
        await Task.Delay(200, cancellationToken);
        return context;
    }
}
