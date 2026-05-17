using llm_agent_demo.Models;

namespace llm_agent_demo.Services.LLM;

/// <summary>
/// LLM 服务统一接口 — 所有提供方（DeepSeek、Ollama 等）都实现此接口。
/// 这是面向接口编程的核心：上层代码只依赖此接口，不关心具体实现。
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// 非流式聊天 — 发送完整消息列表，返回完整响应
    /// </summary>
    /// <param name="request">聊天请求，包含 model、messages、temperature 等</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的聊天响应</returns>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式聊天 — 通过 SSE 实时返回生成内容，适合打字机效果
    /// </summary>
    /// <param name="request">聊天请求（会自动设置 stream=true）</param>
    /// <param name="onChunk">每收到一个文本片段时的回调，参数为增量文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合并后的完整文本</returns>
    Task<string> ChatStreamAsync(ChatRequest request, Action<string> onChunk, CancellationToken cancellationToken = default);
}
