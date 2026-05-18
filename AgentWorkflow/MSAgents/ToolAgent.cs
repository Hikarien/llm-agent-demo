using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace llm_agent_demo.AgentWorkflow.MSAgents;

/// <summary>
/// 工具调用 Agent — 演示 MAF 的 AIFunction 机制。
///
/// AIFunctionFactory.Create() 将 C# 方法注册为 LLM 可调用的函数。
/// ChatClient.AsAIAgent(tools: [...]) 注入工具列表。
/// LLM 自动判断是否需要调用工具，MAF 自动处理序列化和结果回传。
/// </summary>
public class ToolAgent
{
    private readonly AIAgent _agent;
    private AgentSession? _session;

    public ToolAgent(string apiKey)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(GetCurrentTime, name: "get_current_time"),
            AIFunctionFactory.Create(GetWeather, name: "get_weather"),
            AIFunctionFactory.Create(Calculate, name: "calculate")
        };

        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri("https://api.deepseek.com/v1")
            });

        _agent = client.GetChatClient("deepseek-chat").AsAIAgent(
            instructions: """
                你是智能助手，可以调用工具来帮助用户。
                当用户询问时间、天气或计算时，请使用提供的工具函数。
                """,
            name: "ToolAgent",
            tools: tools
        );
    }

    public async Task<string> ChatAsync(string message, CancellationToken ct = default)
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken: ct);
        var response = await _agent.RunAsync(message, _session, cancellationToken: ct);
        return response.ToString();
    }

    // ==================================================================
    // 工具函数
    // ==================================================================

    [Description("获取当前日期和时间，包含时区信息")]
    private static string GetCurrentTime()
    {
        var now = DateTimeOffset.Now;
        return $"当前时间: {now:yyyy-MM-dd HH:mm:ss} ({TimeZoneInfo.Local.DisplayName})";
    }

    [Description("查询指定城市的当前天气")]
    private static string GetWeather(
        [Description("城市名称，如 北京、上海、深圳")] string city)
    {
        return city switch
        {
            "北京" => "北京：晴，25°C，湿度 45%",
            "上海" => "上海：多云，28°C，湿度 65%",
            "深圳" => "深圳：阵雨，30°C，湿度 80%",
            _ => $"{city}：晴间多云，22°C"
        };
    }

    [Description("执行数学计算")]
    private static string Calculate(
        [Description("数学表达式，如 2+2")] string expression)
    {
        try
        {
            var result = new System.Data.DataTable().Compute(expression, null);
            return $"{expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"计算失败: {ex.Message}";
        }
    }
}
