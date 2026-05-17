using llm_agent_demo.Models;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Orchestration;

/// <summary>
/// 流程控制类 — 循环条件判断。
///
/// 判断 Agent 循环（如 ReAct）是否应该继续。
/// 工作流引擎根据输出决定是继续循环还是退出。
///
/// ReAct 模式中典型用法：
///   Think → Act → Observe → LoopCondition（是否继续？）
///
/// 输入上下文 key：  "loop_state"（当前循环状态描述）, "loop_count"（当前循环次数, int）
/// 输出上下文 key：  "loop_action"（"continue" 或 "break"）
/// </summary>
public class LoopConditionStep : IAgentStep
{
    private readonly ILLMService? _llmService;
    private readonly int _maxLoops;

    public string Name => "循环条件判断";

    /// <param name="maxLoops">最大循环次数，防止死循环</param>
    /// <param name="llmService">可选 LLM 服务用于智能判断；为 null 时只用计数判断</param>
    public LoopConditionStep(int maxLoops = 10, ILLMService? llmService = null)
    {
        _maxLoops = maxLoops;
        _llmService = llmService;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        // 获取当前循环次数，首次为 0
        var loopCount = context.TryGetValue("loop_count", out var val) && val is int count
            ? count
            : 0;

        // 超过最大循环次数，强制退出
        if (loopCount >= _maxLoops)
        {
            context["loop_action"] = "break";
            context["loop_reason"] = $"达到最大循环次数 {_maxLoops}";
            return context;
        }

        var loopState = context.GetValueOrDefault("loop_state")?.ToString() ?? "";
        var finalOutput = context.GetValueOrDefault("final_output")?.ToString() ?? "";

        // 如果已经有成功标记，退出
        if (finalOutput.Contains("[TASK_COMPLETE]") || finalOutput.Contains("[FINAL_ANSWER]"))
        {
            context["loop_action"] = "break";
            context["loop_reason"] = "任务完成标记";
            return context;
        }

        // 有 LLM 服务时，让 LLM 判断是否继续
        if (_llmService != null && !string.IsNullOrWhiteSpace(loopState))
        {
            var prompt = $"""
                当前是第 {loopCount + 1} 轮循环。根据以下状态判断是否应该继续：
                {loopState}

                如果任务已经完成或无法继续，回复 "break"。
                如果还需要进一步行动，回复 "continue"。
                只回复一个单词。
                """;

            var request = new ChatRequest
            {
                Model = "deepseek-chat",
                Messages = new List<ChatMessage> { new() { Role = "user", Content = prompt } },
                Temperature = 0.1f,
                MaxTokens = 16
            };

            var response = await _llmService.ChatAsync(request, cancellationToken);
            var decision = response.Choices?.FirstOrDefault()?.Message?.Content?.Trim().ToLower() ?? "continue";
            context["loop_action"] = decision.Contains("break") ? "break" : "continue";
            context["loop_reason"] = $"LLM 判断: {decision}";
        }
        else
        {
            // 无 LLM 时，默认继续直到 maxLoops
            context["loop_action"] = "continue";
            context["loop_reason"] = $"简单计数模式: {loopCount + 1}/{_maxLoops}";
        }

        // 递增循环计数
        context["loop_count"] = loopCount + 1;
        return context;
    }
}
