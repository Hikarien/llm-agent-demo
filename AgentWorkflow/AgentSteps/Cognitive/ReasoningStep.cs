using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;

/// <summary>
/// 认知类 — 推理。
/// 让 LLM 基于给定的事实/前提进行逻辑推理，得出结论。
///
/// 典型场景：给出几条信息，让 LLM 推断结论。
///
/// 输入上下文 key：  "question"（推理问题）, "facts"（事实列表或文本）
/// 输出上下文 key：  "conclusion"
/// </summary>
public class ReasoningStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "推理";

    public ReasoningStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var question = context.GetValueOrDefault("question")?.ToString() ?? "";
        var facts = context.GetValueOrDefault("facts")?.ToString() ?? "";

        var systemPrompt = """
            你是一个逻辑推理助手。请基于以下事实进行推理，回答用户的问题。
            如果事实不足以得出结论，请明确指出。

            推理规则：
            1. 只基于给定事实，不要引入外部知识
            2. 逐步推理，每一步标明依据
            3. 最后给出明确的结论
            """;

        var userContent = $"事实：\n{facts}\n\n问题：{question}";

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userContent }
            },
            Temperature = 0.3f,
            MaxTokens = 1024
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        context["conclusion"] = response.Choices?.FirstOrDefault()?.Message?.Content ?? "无法推理";
        return context;
    }
}
