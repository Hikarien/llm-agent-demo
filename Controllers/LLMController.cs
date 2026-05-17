using Microsoft.AspNetCore.Mvc;
using llm_agent_demo.Models;
using llm_agent_demo.AgentWorkflow;
using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.Controllers;

/// <summary>
/// LLM 智能体 API 控制器 — 提供最基础的 Demo 功能入口。
///
/// 包含三类端点：
///   1. 直接 LLM 调用    — 测试 DeepSeek / Ollama 连接
///   2. 流式 LLM 调用    — 演示 SSE 流式输出
///   3. 智能体工作流    — 演示多步骤 Agent 编排
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LLMController : ControllerBase
{
    private readonly LLMServiceFactory _factory;
    private readonly SimpleAgentWorkflow _workflow;
    private readonly ILogger<LLMController> _logger;

    public LLMController(
        LLMServiceFactory factory,
        SimpleAgentWorkflow workflow,
        ILogger<LLMController> logger)
    {
        _factory = factory;
        _workflow = workflow;
        _logger = logger;
    }

    /// <summary>
    /// [GET] /api/llm/providers — 获取可用的 LLM 提供方列表
    /// </summary>
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        return Ok(new { providers = _factory.GetAvailableProviders() });
    }

    /// <summary>
    /// [POST] /api/llm/chat — 非流式聊天
    ///
    /// 请求体示例：
    /// {
    ///   "provider": "DeepSeek",
    ///   "model": "deepseek-chat",
    ///   "messages": [
    ///     { "role": "system", "content": "你是一个有用的助手" },
    ///     { "role": "user", "content": "你好，请介绍一下自己" }
    ///   ],
    ///   "temperature": 0.7,
    ///   "max_tokens": 1024
    /// }
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequest request,
        [FromQuery] string provider = "DeepSeek",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("收到 Chat 请求: Provider={Provider}, Model={Model}", provider, request.Model);

            var service = _factory.Create(provider);
            var response = await service.ChatAsync(request, cancellationToken);

            _logger.LogInformation("Chat 完成: Tokens={Tokens}", response.Usage?.TotalTokens);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // API Key 未配置等配置错误
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "LLM API 调用失败");
            return StatusCode(502, new { error = $"LLM 服务不可达: {ex.Message}" });
        }
    }

    /// <summary>
    /// [POST] /api/llm/chat/stream — 流式聊天（SSE）
    ///
    /// 响应类型为 text/event-stream，适合前端使用 EventSource 接收。
    ///
    /// curl 测试命令：
    /// curl -N -X POST http://localhost:5000/api/llm/chat/stream?provider=DeepSeek \
    ///   -H "Content-Type: application/json" \
    ///   -d '{"model":"deepseek-chat","messages":[{"role":"user","content":"写一首关于春天的五言绝句"}]}'
    /// </summary>
    [HttpPost("chat/stream")]
    public async Task ChatStream(
        [FromBody] ChatRequest request,
        [FromQuery] string provider = "DeepSeek",
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var service = _factory.Create(provider);

        try
        {
            await service.ChatStreamAsync(request, chunk =>
            {
                // 按 SSE 格式写入流：data: <内容>\n\n
                var sseData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = chunk })}\n\n";
                Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(sseData), cancellationToken)
                    .AsTask().Wait(cancellationToken);
                Response.Body.FlushAsync(cancellationToken).Wait(cancellationToken);
            }, cancellationToken);

            // 发送结束标记
            await Response.Body.WriteAsync(
                System.Text.Encoding.UTF8.GetBytes("data: [DONE]\n\n"), cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式调用异常");
            var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
            await Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(errorData), cancellationToken);
        }
    }

    /// <summary>
    /// [POST] /api/llm/agent/workflow — 执行智能体工作流
    ///
    /// 请求体示例：
    /// {
    ///   "userInput": "帮我解释什么是量子纠缠",
    ///   "provider": "DeepSeek",
    ///   "model": "deepseek-chat",
    ///   "debug": true
    /// }
    ///
    /// 会依次执行：意图分析 → 生成回复 → 验证输出
    /// </summary>
    [HttpPost("agent/workflow")]
    public async Task<IActionResult> RunWorkflow(
        [FromBody] WorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "开始执行工作流: Provider={Provider}, Model={Model}, Input={Input}",
                request.Provider, request.Model, request.UserInput);

            var response = await _workflow.ExecuteAsync(
                request.UserInput,
                request.Debug,
                cancellationToken);

            _logger.LogInformation("工作流完成: 耗时={Elapsed}ms", response.ElapsedMs);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "工作流中 LLM 调用失败");
            return StatusCode(502, new { error = $"LLM 服务不可达: {ex.Message}" });
        }
    }

}
