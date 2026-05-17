using System.Text.Json;
using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Orchestration;

/// <summary>
/// 流程控制类 — 合并。
///
/// 将多个并行分支的结果合并为统一输出。
/// 例如：同时搜索多个数据源，然后合并去重。
///
/// 输入上下文 key：  "branch_results"（List&lt;string&gt; 或 JSON 数组字符串）
/// 输出上下文 key：  "merged_result"
/// </summary>
public class MergeStep : IAgentStep
{
    private readonly ILLMService? _llmService;

    public string Name => "合并";

    /// <param name="llmService">可选 LLM 服务用于智能合并；为 null 时仅简单拼接</param>
    public MergeStep(ILLMService? llmService = null)
    {
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        // 尝试从上下文获取分支结果
        var results = new List<string>();

        if (context.TryGetValue("branch_results", out var value))
        {
            results = value switch
            {
                List<string> list => list,
                string json => ParseJsonArray(json),
                _ => new List<string> { value.ToString() ?? "" }
            };
        }

        if (results.Count == 0)
        {
            context["merged_result"] = "(无分支结果)";
            return context;
        }

        if (results.Count == 1)
        {
            context["merged_result"] = results[0];
            return context;
        }

        // 有 LLM 时，让 LLM 智能合并
        if (_llmService != null)
        {
            var branches = string.Join("\n---\n", results.Select((r, i) => $"分支 {i + 1}:\n{r}"));

            var request = new ChatRequest
            {
                Model = "deepseek-chat",
                Messages = new List<ChatMessage>
                {
                    new()
                    {
                        Role = "system",
                        Content = "你是一个信息整合助手。请将以下多个分支的结果合并为一份连贯的回复，去重并保持信息完整。"
                    },
                    new() { Role = "user", Content = branches }
                },
                Temperature = 0.3f,
                MaxTokens = 1024
            };

            var response = await _llmService.ChatAsync(request, cancellationToken);
            context["merged_result"] = response.Choices?.FirstOrDefault()?.Message?.Content ?? string.Join("\n\n", results);
        }
        else
        {
            // 无 LLM：简单拼接
            context["merged_result"] = string.Join("\n\n---\n\n", results);
        }

        return context;
    }

    private static List<string> ParseJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string> { json };
        }
    }
}
