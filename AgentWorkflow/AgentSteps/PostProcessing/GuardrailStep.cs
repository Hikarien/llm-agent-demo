using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.PostProcessing;

/// <summary>
/// 后处理类 — 安全护栏。
///
/// 对 LLM 生成的内容做安全检查，拦截不当内容。
/// 通常放在 Final Output 输出前最后一步。
///
/// 输入上下文 key：  "content"（待检查内容，fallback 到 "draft" / "final_output"）
/// 输出上下文 key：  "guardrail_result"（"pass" / "block"）
///                    "guardrail_reason"（拦截原因）
///                    "safe_content"（通过时的清洁内容，拦截时为空）
/// </summary>
public class GuardrailStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "安全护栏";

    public GuardrailStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var content = context.GetValueOrDefault("content")?.ToString()
                   ?? context.GetValueOrDefault("final_output")?.ToString()
                   ?? context.GetValueOrDefault("draft")?.ToString()
                   ?? "";

        var systemPrompt = """
            你是一个内容安全审核员。检查以下内容是否包含：

            1. 暴力、恐怖主义内容
            2. 色情、低俗内容
            3. 仇恨言论、歧视
            4. 违法犯罪指导
            5. 个人隐私信息（身份证号、手机号、地址等）

            如果安全，回复 "PASS" + 原内容。
            如果有问题，回复 "BLOCK: <具体原因>"。
            不要加其他解释。
            """;

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = content }
            },
            Temperature = 0.1f,
            MaxTokens = 256
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        var result = response.Choices?.FirstOrDefault()?.Message?.Content ?? "";

        if (result.StartsWith("PASS", StringComparison.OrdinalIgnoreCase))
        {
            context["guardrail_result"] = "pass";
            context["guardrail_reason"] = "";
            context["safe_content"] = result[4..].Trim();
        }
        else
        {
            context["guardrail_result"] = "block";
            context["guardrail_reason"] = result.Replace("BLOCK:", "").Trim();
            context["safe_content"] = "";
        }

        return context;
    }
}
