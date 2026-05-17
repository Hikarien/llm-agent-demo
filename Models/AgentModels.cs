namespace llm_agent_demo.Models;

/// <summary>
/// 智能体工作流请求
/// </summary>
public class WorkflowRequest
{
    /// <summary>用户输入的原始问题</summary>
    public string UserInput { get; set; } = "";

    /// <summary>使用的 LLM 提供方：DeepSeek / Ollama</summary>
    public string Provider { get; set; } = "DeepSeek";

    /// <summary>使用的模型名称</summary>
    public string Model { get; set; } = "deepseek-chat";

    /// <summary>是否启用调试输出（展示每一步的中间结果）</summary>
    public bool Debug { get; set; } = false;
}

/// <summary>
/// 智能体工作流响应
/// </summary>
public class WorkflowResponse
{
    /// <summary>最终结果</summary>
    public string FinalOutput { get; set; } = "";

    /// <summary>执行耗时（毫秒）</summary>
    public long ElapsedMs { get; set; }

    /// <summary>每个步骤的执行记录（Debug 模式下返回）</summary>
    public List<StepRecord> Steps { get; set; } = new();
}

/// <summary>
/// 单个步骤的执行记录
/// </summary>
public class StepRecord
{
    /// <summary>步骤名称</summary>
    public string StepName { get; set; } = "";

    /// <summary>该步骤耗时（毫秒）</summary>
    public long ElapsedMs { get; set; }

    /// <summary>输入摘要</summary>
    public string InputSummary { get; set; } = "";

    /// <summary>输出摘要</summary>
    public string OutputSummary { get; set; } = "";
}
