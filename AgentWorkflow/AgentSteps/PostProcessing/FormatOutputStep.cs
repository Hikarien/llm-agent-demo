using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.PostProcessing;

/// <summary>
/// 后处理类 — 格式化输出。
///
/// 将原始输出按指定格式（Markdown / JSON / 纯文本）重新组织。
/// 支持通过 LLM 格式化（智能）或本地规则格式化（快速）。
///
/// 输入上下文 key：  "raw_output"（或 "draft" / "final_output"）
///                    "format"（目标格式: "markdown" / "json" / "text"，默认 "markdown"）
/// 输出上下文 key：  "formatted_output"
/// </summary>
public class FormatOutputStep : IAgentStep
{
    private readonly ILLMService? _llmService;

    public string Name => "格式化输出";

    /// <param name="llmService">可选 LLM 服务用于智能格式化；为 null 时仅做简单包装</param>
    public FormatOutputStep(ILLMService? llmService = null)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var raw = context.GetValueOrDefault("raw_output")?.ToString()
               ?? context.GetValueOrDefault("final_output")?.ToString()
               ?? context.GetValueOrDefault("draft")?.ToString()
               ?? "";

        var format = context.GetValueOrDefault("format")?.ToString()?.ToLower() ?? "markdown";

        // 简单场景（短文本或无 LLM）直接本地格式化
        if (_llmService == null || raw.Length < 100)
        {
            context["formatted_output"] = format switch
            {
                "json" => $$"""{"output": "{{raw.Replace("\"", "\\\"")}}"}""",
                "markdown" => $"## 回复\n\n{raw}",
                _ => raw
            };
            return context;
        }

        // 有 LLM 时，让 LLM 按要求格式化
        var formatInstructions = format switch
        {
            "json" => "将以下内容转换为格式良好的 JSON 对象，不要加解释。",
            "markdown" => "将以下内容重新排版为 Markdown 格式，加上合适的标题和列表。不要加额外解释。",
            "text" => "将以下内容精简为纯文本，去除所有格式标记。",
            _ => "将以下内容优化排版。"
        };

        var request = new ChatRequest
        {
            Model = "deepseek-chat",
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = formatInstructions },
                new() { Role = "user", Content = raw }
            },
            Temperature = 0.2f,
            MaxTokens = 1024
        };

        var response = await _llmService.ChatAsync(request, cancellationToken);
        context["formatted_output"] = response.Choices?.FirstOrDefault()?.Message?.Content ?? raw;
        return context;
    }
}
