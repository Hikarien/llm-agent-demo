using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;

/// <summary>
/// 认知类 — 意图分析。
/// 将用户自然语言输入发给 LLM，识别意图和情绪倾向。
///
/// 输入上下文 key：  "user_input"
/// 输出上下文 key：  "intent"
/// </summary>
public class IntentAnalysisStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "意图分析";

    public IntentAnalysisStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var userInput = context.GetValueOrDefault("user_input")?.ToString() ?? "";

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = "你是一个意图分析助手。请用简洁的一句话分析用户意图，包括：用户想做什么、情绪倾向（积极/消极/中性）。不超过 50 字。"
                },
                new() { Role = "user", Content = userInput }
            },
            Temperature = 0.3f,
            MaxTokens = 128
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        context["intent"] = response.Choices?.FirstOrDefault()?.Message?.Content ?? "无法分析意图";
        return context;
    }
}
