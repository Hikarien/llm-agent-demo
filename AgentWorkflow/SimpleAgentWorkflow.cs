using System.Diagnostics;
using llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;
using llm_agent_demo.AgentWorkflow.AgentSteps.PostProcessing;
using llm_agent_demo.Models;

namespace llm_agent_demo.AgentWorkflow;

/// <summary>
/// 示例智能体工作流 — 按顺序执行多个步骤，每个步骤共享一个上下文字典。
///
/// 默认工作流程（三步）：
///   1. 意图分析  — Cognitive/IntentAnalysisStep
///   2. 生成回复  — Cognitive/GenerationStep
///   3. 验证输出  — PostProcessing/ValidateOutputStep
///
/// 更多可用步骤参考 AgentSteps/ 下的四个分类：
///   Cognitive/       — 认知类（思考）
///   Action/          — 行动类（执行）
///   Orchestration/   — 流程控制类（编排）
///   PostProcessing/  — 后处理类（格式与安全）
///
/// 这演示了 LLM Agent 的核心模式：
///   - 步骤串联（Chain）
///   - 上下文传递（Context/State）
///   - 提示词嵌套（把上一步的输出用作下一步的输入）
///
/// 扩展方向：
///   - 加入条件分支（根据意图选择不同的后续步骤）
///   - 加入工具调用（Tool Calling，让 LLM 调用外部 API）
///   - 加入循环（ReAct 模式：思考 → 行动 → 观察 → 思考...）
/// </summary>
public class SimpleAgentWorkflow
{
    private readonly IEnumerable<IAgentStep> _steps;

    public SimpleAgentWorkflow(IEnumerable<IAgentStep> steps)
    {
        // DI 容器会自动注入所有实现了 IAgentStep 的类
        // OrderBy 通过 switch 指定执行顺序，未匹配的排到最后
        _steps = steps.OrderBy(s => s switch
        {
            IntentAnalysisStep => 1,
            GenerationStep => 2,
            ValidateOutputStep => 3,
            _ => 99
        });
    }

    /// <summary>
    /// 执行完整工作流
    /// </summary>
    public async Task<WorkflowResponse> ExecuteAsync(
        string userInput,
        bool debug = false,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var context = new Dictionary<string, object>
        {
            ["user_input"] = userInput
        };
        var records = new List<StepRecord>();

        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stepSw = Stopwatch.StartNew();

            var inputKeys = string.Join(", ", context.Keys);
            context = await step.ExecuteAsync(context, cancellationToken);
            stepSw.Stop();

            if (debug)
            {
                records.Add(new StepRecord
                {
                    StepName = step.Name,
                    ElapsedMs = stepSw.ElapsedMilliseconds,
                    InputSummary = $"上下文包含 {context.Count} 个键: {inputKeys}",
                    OutputSummary = GetContextSummary(context)
                });
            }
        }

        sw.Stop();
        return new WorkflowResponse
        {
            FinalOutput = context.GetValueOrDefault("final_output")?.ToString()
                          ?? context.GetValueOrDefault("draft")?.ToString()
                          ?? "工作流未生成输出",
            ElapsedMs = sw.ElapsedMilliseconds,
            Steps = records
        };
    }

    private static string GetContextSummary(Dictionary<string, object> context)
    {
        var summaries = new List<string>();
        foreach (var (key, value) in context)
        {
            var text = value?.ToString() ?? "(null)";
            if (text.Length > 100)
                text = text[..100] + "...";
            summaries.Add($"{key}: {text}");
        }
        return string.Join(" | ", summaries);
    }
}
