using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;

/// <summary>
/// 认知类 — 摘要。
/// 让 LLM 将长文本压缩为简洁摘要。
///
/// 典型场景：将一篇长文章/文档压缩为 3 句话的要点。
///
/// 输入上下文 key：  "long_text"
/// 输出上下文 key：  "summary"
/// </summary>
public class SummarizationStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "摘要";

    public SummarizationStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var longText = context.GetValueOrDefault("long_text")?.ToString()
                    ?? context.GetValueOrDefault("user_input")?.ToString()
                    ?? "";

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = "你是一个文本摘要助手。请用 3~5 句话总结以下内容的核心要点，保持简洁准确。"
                },
                new() { Role = "user", Content = longText }
            },
            Temperature = 0.3f,
            MaxTokens = 512
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        context["summary"] = response.Choices?.FirstOrDefault()?.Message?.Content ?? "无法生成摘要";
        return context;
    }
}
