namespace llm_agent_demo.AgentWorkflow.AgentSteps.Action;

/// <summary>
/// 工具调用委托 — 接收参数字典，返回执行结果字符串
/// </summary>
public delegate Task<string> ToolFunction(Dictionary<string, object> args);

/// <summary>
/// 行动类 — 工具调用。
///
/// 根据上下文中指定的 tool_name 分发到注册的工具函数执行。
/// 这是 Agent Tool Calling 的简化实现（不依赖 LLM 的 native function calling，
/// 而是由上游步骤决定调用哪个工具）。
///
/// 使用方式：
///   var step = new ToolCallStep();
///   step.RegisterTool("get_weather", async args => {
///       var city = args["city"].ToString();
///       return $"今天{city}的天气：晴，25°C";
///   });
///
/// 输入上下文 key：  tool_name（工具名）, tool_args（Dictionary）
/// 输出上下文 key：  tool_result
/// </summary>
public class ToolCallStep : IAgentStep
{
    private readonly Dictionary<string, ToolFunction> _tools = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "工具调用";

    /// <summary>注册一个工具函数</summary>
    public ToolCallStep RegisterTool(string name, ToolFunction function)
    {
        _tools[name] = function;
        return this;
    }

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var toolName = context.GetValueOrDefault("tool_name")?.ToString() ?? "";
        var toolArgs = context.GetValueOrDefault("tool_args") as Dictionary<string, object>
                    ?? new Dictionary<string, object>();

        if (!_tools.TryGetValue(toolName, out var tool))
        {
            var available = string.Join(", ", _tools.Keys);
            context["tool_result"] = $"[错误] 未找到工具 '{toolName}'。可用工具: {available}";
            return context;
        }

        try
        {
            var result = await tool(toolArgs);
            context["tool_result"] = result;
        }
        catch (Exception ex)
        {
            context["tool_result"] = $"[工具执行异常] {ex.Message}";
        }

        return context;
    }
}
