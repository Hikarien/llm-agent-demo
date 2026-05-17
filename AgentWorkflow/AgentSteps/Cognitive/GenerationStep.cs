using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;

/// <summary>
/// 认知类 — 生成回复。
/// 根据用户输入和意图分析结果，让 LLM 生成回复草稿。
///
/// 输入上下文 key：  "user_input", "intent"（可选）
/// 输出上下文 key：  "draft"
/// </summary>
public class GenerationStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "生成回复";

    public GenerationStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var userInput = context.GetValueOrDefault("user_input")?.ToString() ?? "";
        var intent = context.GetValueOrDefault("intent")?.ToString() ?? "";

        var systemPrompt = intent.Length > 0
            ? $"你是一个乐于助人的 AI 助手。根据用户意图：\"{intent}\"，请生成有帮助的回复。"
            : "你是一个乐于助人的 AI 助手。请生成有帮助的回复。";

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userInput }
            },
            Temperature = 0.7f,
            MaxTokens = 1024
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        context["draft"] = response.Choices?.FirstOrDefault()?.Message?.Content ?? "无法生成回复";
        return context;
    }
}
