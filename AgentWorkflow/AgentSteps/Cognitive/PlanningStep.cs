using System.Text.Json;
using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;

/// <summary>
/// 认知类 — 任务规划。
/// 让 LLM 将一个复杂目标拆解为有序的子任务列表。
///
/// 典型场景：用户说"帮我写一份市场调研报告" →
///   LLM 拆解为 ["确定报告主题", "收集数据", "分析竞品", "写初稿", "润色"]
///
/// 输入上下文 key：  "goal"（目标描述）
/// 输出上下文 key：  "plan"（JSON 字符串数组，如 ["步骤1","步骤2"]）
/// </summary>
public class PlanningStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "任务规划";

    public PlanningStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        // 优先取 "goal"，fallback 到 "user_input"
        var goal = context.GetValueOrDefault("goal")?.ToString()
                ?? context.GetValueOrDefault("user_input")?.ToString()
                ?? "";

        var systemPrompt = """
            你是一个任务规划专家。请将用户的目标拆解为 3~5 个有序步骤。
            严格按以下 JSON 数组格式返回，不要加解释：
            ["步骤1", "步骤2", "步骤3"]
            """;

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = goal }
            },
            Temperature = 0.3f,
            MaxTokens = 512
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        var raw = response.Choices?.FirstOrDefault()?.Message?.Content ?? "[]";

        // 尝试解析为 JSON 数组，失败则原样存储
        try
        {
            var plan = JsonSerializer.Deserialize<List<string>>(raw);
            context["plan"] = plan ?? new List<string> { raw };
        }
        catch
        {
            context["plan"] = new List<string> { raw };
        }

        return context;
    }
}
