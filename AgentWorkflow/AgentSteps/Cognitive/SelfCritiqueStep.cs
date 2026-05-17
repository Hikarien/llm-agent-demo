using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;

/// <summary>
/// 认知类 — 自我反思。
/// 让 LLM 评价自己（或其他 LLM）生成的草稿质量，指出问题和改进方向。
///
/// 这是提升 Agent 输出质量的关键技术之一（Self-Reflection / Self-Critique）。
///
/// 输入上下文 key：  "draft"（或 "final_output"）
/// 输出上下文 key：  "critique"（评审意见）
/// </summary>
public class SelfCritiqueStep : IAgentStep
{
    private readonly ILLMService _llmService;
    public string Name => "自我反思";

    public SelfCritiqueStep(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var draft = context.GetValueOrDefault("draft")?.ToString()
                 ?? context.GetValueOrDefault("final_output")?.ToString()
                 ?? "";

        var systemPrompt = """
            你是一个内容评审专家。请评审以下 AI 生成的内容，从以下几个维度打分（1~10）并给出改进建议：

            1. 准确性 — 内容是否准确可靠
            2. 完整性 — 是否充分回答了问题
            3. 清晰性 — 表达是否清晰易懂
            4. 安全性 — 是否有不当或有害内容

            最后给出总体评价：PASS（合格）/ REVISE（需要修改）/ REJECT（不合格）。
            格式要求：简洁，总计不超过 150 字。
            """;

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = $"待评审内容：\n{draft}" }
            },
            Temperature = 0.3f,
            MaxTokens = 256
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        context["critique"] = response.Choices?.FirstOrDefault()?.Message?.Content ?? "无法评审";
        return context;
    }
}
