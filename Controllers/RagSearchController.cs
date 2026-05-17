using Microsoft.AspNetCore.Mvc;
using llm_agent_demo.AgentWorkflow;
using llm_agent_demo.Models;

namespace llm_agent_demo.Controllers;

/// <summary>
/// RAG 检索控制器 — 负责 RAG 流程的预处理、检索与增强。
///
/// 包含端点：
///   1. 意图提取 + 查询分解 — 将用户自然语言拆解为向量检索子查询
/// </summary>
[ApiController]
[Route("api/rag")]
public class RagSearchController : ControllerBase
{
    private readonly RAGProcessingAgent _ragAgent;
    private readonly ILogger<RagSearchController> _logger;

    public RagSearchController(RAGProcessingAgent ragAgent, ILogger<RagSearchController> logger)
    {
        _ragAgent = ragAgent;
        _logger = logger;
    }

    /// <summary>
    /// [POST] /api/rag/preprocess — RAG 预处理：意图提取 + 查询分解
    ///
    /// 将用户的自然语言问题拆解为适合向量检索的最小查询子集。
    ///
    /// 请求体示例：
    /// {
    ///   "userInput": "对比.NET 10和Spring Boot在微服务下的性能差异"
    /// }
    ///
    /// 返回示例：
    /// {
    ///   "intent": "技术对比：.NET 10 vs Spring Boot 微服务性能",
    ///   "subQueries": [
    ///     ".NET 10 微服务性能基准测试",
    ///     "Spring Boot 微服务性能基准测试",
    ///     ".NET 10 Spring Boot 性能对比 benchmark",
    ///     ".NET 10 微服务吞吐量 启动时间",
    ///     "Spring Boot 微服务吞吐量 启动时间"
    ///   ],
    ///   "queryCount": 5,
    ///   "elapsedMs": 2341
    /// }
    /// </summary>
    [HttpPost("preprocess")]
    public async Task<IActionResult> Preprocess(
        [FromBody] WorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("RAG 预处理: Input={Input}", request.UserInput);

            var context = new Dictionary<string, object>
            {
                ["user_input"] = request.UserInput
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            context = await _ragAgent.ExecuteAsync(context, cancellationToken);
            sw.Stop();

            var subQueries = context.GetValueOrDefault("rag_sub_queries") as List<string>
                          ?? new List<string>();

            return Ok(new
            {
                intent = context.GetValueOrDefault("rag_intent")?.ToString() ?? "",
                subQueries = subQueries,
                queryCount = subQueries.Count,
                elapsedMs = sw.ElapsedMilliseconds
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "RAG 预处理中 LLM 调用失败");
            return StatusCode(502, new { error = $"LLM 服务不可达: {ex.Message}" });
        }
    }
}
