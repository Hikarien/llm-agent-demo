using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.PostProcessing;

/// <summary>
/// 后处理类 — 兜底回复。
///
/// 当工作流中某步出错或 LLM 调用失败时，生成友好的兜底回复，
/// 避免用户看到技术错误信息。
///
/// 输入上下文 key：  "error"（错误信息）, "user_input"（原始问题）
/// 输出上下文 key：  "recovery_output", "final_output"（覆盖）
/// </summary>
public class FallbackStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "兜底回复";

    public FallbackStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var error = context.GetValueOrDefault("error")?.ToString() ?? "";
        var userInput = context.GetValueOrDefault("user_input")?.ToString() ?? "";

        // 如果 LLM 还可达，让它生成友好的道歉回复
        try
        {
            var request = new ChatRequest
            {
                Model = "deepseek-chat",
                Messages = new List<ChatMessage>
                {
                    new()
                    {
                        Role = "system",
                        Content = "你是一个客服助手。处理请求时遇到了技术问题，请生成一句友好的道歉回复（不超过 50 字），告诉用户暂时无法处理，并建议稍后重试。"
                    },
                    new() { Role = "user", Content = $"用户原问题: {userInput}\n错误: {error}" }
                },
                Temperature = 0.7f,
                MaxTokens = 128
            };

            var response = await _llmService.ChatAsync(request, cancellationToken);
            var recovery = response.Choices?.FirstOrDefault()?.Message?.Content ?? GetStaticFallback();
            context["recovery_output"] = recovery;
            context["final_output"] = recovery;
        }
        catch
        {
            // LLM 也不可达时，返回静态兜底
            var staticFallback = GetStaticFallback();
            context["recovery_output"] = staticFallback;
            context["final_output"] = staticFallback;
        }

        return context;
    }

    private static string GetStaticFallback()
    {
        return "抱歉，服务暂时不可用，请稍后重试。如问题持续，请联系技术支持。";
    }
}
