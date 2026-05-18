using Microsoft.AspNetCore.Mvc;
using llm_agent_demo.AgentWorkflow.MSAgents;

namespace llm_agent_demo.Controllers;

/// <summary>
/// Microsoft Agent Framework (MAF) Demo 控制器。
///
/// MAF 是 Microsoft 2026 年 4 月正式 GA 的新一代 Agent 框架，
/// 提供统一的 AI Agent 编程模型，支持单 Agent、多 Agent、A2A 协议。
///
/// 演示三大能力：
///   1. ChatClientAgent — 基础对话（Session 管理 + 流式）
///   2. AIFunction     — 工具调用（C# 方法自动暴露为 LLM Function）
///   3. Multi-Agent    — 多 Agent 协作评审
/// </summary>
[ApiController]
[Route("api/ms-agent")]
public class MSAgentDemoController : ControllerBase
{
    private readonly string _apiKey;
    private readonly ILogger<MSAgentDemoController> _logger;

    public MSAgentDemoController(IConfiguration configuration, ILogger<MSAgentDemoController> logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
               ?? configuration["LlmConfig:DeepSeek:ApiKey"]
               ?? "";
        _logger = logger;
    }

    // ======================================================================
    // 1. 基础对话 Agent（Session 管理）
    // ======================================================================

    /// <summary>
    /// [POST] /api/ms-agent/chat — 单轮基础对话
    ///
    /// 请求体：
    /// { "message": "你好，介绍一下 MAF 框架", "systemPrompt": "你是 AI 助手" }
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] MsAgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return BadRequest(new { error = "API Key 未配置" });

        try
        {
            var agent = new DeepSeekChatAgent(_apiKey, request.SystemPrompt);
            var reply = await agent.ChatOnceAsync(request.Message, ct);

            return Ok(new { reply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MAF Chat 失败");
            return StatusCode(502, new { error = ex.Message });
        }
    }

    /// <summary>
    /// [POST] /api/ms-agent/session-chat — 多轮对话（带 Session 上下文记忆）
    ///
    /// 使用 sessionId 标识同一会话，同一个 sessionId 的请求共享上下文。
    /// 不传 sessionId 或传空字符串则创建新会话。
    ///
    /// 请求体：
    /// { "message": "我叫张三", "sessionId": "" }
    /// </summary>
    [HttpPost("session-chat")]
    public async Task<IActionResult> SessionChat([FromBody] MsAgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return BadRequest(new { error = "API Key 未配置" });

        try
        {
            // 从内存字典中获取或创建 Agent
            var key = request.SessionId ?? Guid.NewGuid().ToString();
            if (!_agents.TryGetValue(key, out var agent))
            {
                agent = new DeepSeekChatAgent(_apiKey, request.SystemPrompt);
                _agents[key] = agent;
            }

            var reply = await agent.ChatWithSessionAsync(request.Message, ct);

            return Ok(new { reply, sessionId = key });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MAF SessionChat 失败");
            return StatusCode(502, new { error = ex.Message });
        }
    }

    /// <summary>
    /// [POST] /api/ms-agent/chat/stream — 流式对话
    /// </summary>
    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] MsAgentRequest request, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            var err = System.Text.Json.JsonSerializer.Serialize(new { error = "API Key 未配置" });
            await Response.Body.WriteAsync(
                System.Text.Encoding.UTF8.GetBytes($"data: {err}\n\n"), ct);
            return;
        }

        try
        {
            var agent = new DeepSeekChatAgent(_apiKey, request.SystemPrompt);
            await foreach (var chunk in agent.ChatStreamAsync(request.Message))
            {
                var data = System.Text.Json.JsonSerializer.Serialize(new { content = chunk });
                await Response.Body.WriteAsync(
                    System.Text.Encoding.UTF8.GetBytes($"data: {data}\n\n"), ct);
                await Response.Body.FlushAsync(ct);
            }
            await Response.Body.WriteAsync(
                System.Text.Encoding.UTF8.GetBytes("data: [DONE]\n\n"), ct);
        }
        catch (Exception ex)
        {
            var err = System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message });
            await Response.Body.WriteAsync(
                System.Text.Encoding.UTF8.GetBytes($"data: {err}\n\n"), ct);
        }
    }

    // ======================================================================
    // 2. 工具调用 Agent
    // ======================================================================

    /// <summary>
    /// [POST] /api/ms-agent/tool-call — 自动工具调用
    ///
    /// MAF 通过 AIFunctionFactory.Create() 将 C# 方法注册为 LLM 工具。
    /// 内置三个工具：get_current_time / get_weather / calculate
    ///
    /// 测试用例：
    ///   "现在几点了？"            → 自动调用 get_current_time
    ///   "北京天气怎么样？"        → 自动调用 get_weather
    ///   "2+2 等于多少？"          → 自动调用 calculate
    /// </summary>
    [HttpPost("tool-call")]
    public async Task<IActionResult> ToolCall([FromBody] MsAgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return BadRequest(new { error = "API Key 未配置" });

        try
        {
            var agent = new ToolAgent(_apiKey);
            var reply = await agent.ChatAsync(request.Message, ct);

            return Ok(new { reply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MAF ToolCall 失败");
            return StatusCode(502, new { error = ex.Message });
        }
    }

    // ======================================================================
    // 3. 多 Agent 协作
    // ======================================================================

    /// <summary>
    /// [POST] /api/ms-agent/multi-review — 多 Agent 技术评审
    ///
    /// 架构师 + 安全专家 并行审查 → Orchestrator 汇总报告。
    ///
    /// 请求体：
    /// { "message": "计划将单体应用拆分为微服务，用 K8s 部署，DB 迁移到 PostgreSQL" }
    /// </summary>
    [HttpPost("multi-review")]
    public async Task<IActionResult> MultiReview([FromBody] MsAgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return BadRequest(new { error = "API Key 未配置" });

        try
        {
            var reviewer = new MultiAgentReviewer(_apiKey);
            var result = await reviewer.ReviewAsync(request.Message, ct);

            return Ok(new
            {
                architectureOpinion = result.ArchitectureOpinion,
                securityOpinion = result.SecurityOpinion,
                summary = result.Summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MAF MultiReview 失败");
            return StatusCode(502, new { error = ex.Message });
        }
    }

    // Session 内存存储（生产环境应替换为 Redis / EF Core）
    private static readonly Dictionary<string, DeepSeekChatAgent> _agents = new();
}

/// <summary>MS Agent Demo 请求模型</summary>
public class MsAgentRequest
{
    public string Message { get; set; } = "";
    public string? SystemPrompt { get; set; }
    public string? SessionId { get; set; }
}
