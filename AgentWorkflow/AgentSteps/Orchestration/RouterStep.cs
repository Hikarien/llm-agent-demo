using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Orchestration;

/// <summary>
/// 流程控制类 — 路由。
///
/// 根据意图分析结果（或任意分类键）决定下一步走哪个分支。
/// 不会修改主流程输出，只在上下文中写入 "next_step" 供外部工作流引擎读取。
///
/// 典型使用：工作流引擎（如 ReAct）读取 "next_step" 后动态选择后续步骤。
///
/// 输入上下文 key：  "intent"（或 "route_key"）
/// 输出上下文 key：  "next_step"（建议的下一步名称）, "route_reason"（路由理由）
/// </summary>
public class RouterStep : IAgentStep
{
    private readonly Dictionary<string, string> _routes;
    private readonly ILLMService? _llmService;

    public string Name => "路由";

    /// <summary>
    /// 构造路由步骤
    /// </summary>
    /// <param name="routes">静态路由表：key=意图关键词, value=目标步骤名</param>
    /// <param name="llmService">可选的 LLM 服务，用于动态路由（传 null 则只用静态匹配）</param>
    public RouterStep(Dictionary<string, string>? routes = null, ILLMService? llmService = null)
    {
        _routes = routes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var intent = context.GetValueOrDefault("intent")?.ToString()
                  ?? context.GetValueOrDefault("route_key")?.ToString()
                  ?? "";

        // 先尝试静态路由表精确匹配
        foreach (var (keyword, target) in _routes)
        {
            if (intent.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                context["next_step"] = target;
                context["route_reason"] = $"静态路由匹配关键词 '{keyword}' → {target}";
                return context;
            }
        }

        // 静态未命中且有 LLM 服务时，用 LLM 做动态路由
        if (_llmService != null)
        {
            var targetSteps = _routes.Values.Distinct().ToList();
            if (targetSteps.Count > 0)
            {
                var prompt = $"""
                    根据用户意图 "{intent}"，从以下步骤中选择最合适的一个。
                    只返回步骤名称，不要加解释。
                    可选步骤: {string.Join(", ", targetSteps)}
                    """;

                var request = new ChatRequest
                {
                    Model = "deepseek-chat",
                    Messages = new List<ChatMessage>
                    {
                        new() { Role = "user", Content = prompt }
                    },
                    Temperature = 0.1f,
                    MaxTokens = 64
                };

                var response = await _llmService.ChatAsync(request, cancellationToken);
                var nextStep = response.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "unknown";
                context["next_step"] = nextStep;
                context["route_reason"] = $"LLM 动态路由: {intent} → {nextStep}";
                return context;
            }
        }

        // 完全没有匹配
        context["next_step"] = "unknown";
        context["route_reason"] = $"未找到匹配路由，意图: {intent}";
        return context;
    }
}
