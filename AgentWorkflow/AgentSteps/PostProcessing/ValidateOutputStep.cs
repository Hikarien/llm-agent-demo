using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.PostProcessing;

/// <summary>
/// 后处理类 — 验证输出。
/// 让 LLM 自我检查生成的内容质量。
///
/// 输入上下文 key：  "user_input", "draft"
/// 输出上下文 key：  "final_output"
/// </summary>
public class ValidateOutputStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "验证输出";

    public ValidateOutputStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var userInput = context.GetValueOrDefault("user_input")?.ToString() ?? "";
        var draft = context.GetValueOrDefault("draft")?.ToString() ?? "";

        var systemPrompt = $"""
            你是一个内容审核助手。请检查以下 AI 回复对用户问题的回答质量：

            用户问题：{userInput}

            AI 回复草稿：
            {draft}

            请判断：如果内容合理、切题、无明显错误，直接原样返回草稿内容。
            如果有轻微问题，修正后返回。
            如果完全不相关，返回 [INVALID] 并说明原因。
            只返回最终内容，不要加解释。
            """;

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = systemPrompt }
            },
            Temperature = 0.2f,
            MaxTokens = 1024
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        var validated = response.Choices?.FirstOrDefault()?.Message?.Content ?? draft;

        context["final_output"] = validated;
        return context;
    }
}
