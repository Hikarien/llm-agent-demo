using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace llm_agent_demo.AgentWorkflow.MSAgents;

/// <summary>
/// 基于 MAF 的 DeepSeek 对话 Agent。
///
/// 使用 ChatClient.AsAIAgent() 一行创建 Agent，MAF 自动处理：
///   - ChatHistory 管理（通过 AgentSession）
///   - Function Calling 循环
///   - 流式 + 非流式双模式
///
/// 核心调用链：
///   OpenAIClient → ChatClient → .AsAIAgent() → agent.RunAsync()
/// </summary>
public class DeepSeekChatAgent
{
    private readonly AIAgent _agent;
    private AgentSession? _session;

    public DeepSeekChatAgent(string apiKey, string? instructions = null, IList<AITool>? tools = null)
    {
        // Step 1: 创建 OpenAI ChatClient，指向 DeepSeek
        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri("https://api.deepseek.com/v1")
            });
        var chatClient = client.GetChatClient("deepseek-chat");

        // Step 2: MAF 扩展方法 — ChatClient 直接变为 AIAgent
        _agent = chatClient.AsAIAgent(
            instructions: instructions,
            name: "DeepSeekAgent",
            description: "基于 DeepSeek 的 MAF 对话 Agent",
            tools: tools
        );
    }

    /// <summary>单轮对话</summary>
    public async Task<string> ChatOnceAsync(string message, CancellationToken ct = default)
    {
        var response = await _agent.RunAsync(message, cancellationToken: ct);
        return response.ToString();
    }

    /// <summary>多轮对话（Session 保留上下文）</summary>
    public async Task<string> ChatWithSessionAsync(string message, CancellationToken ct = default)
    {
        _session ??= await _agent.CreateSessionAsync(cancellationToken: ct);
        var response = await _agent.RunAsync(message, _session, cancellationToken: ct);
        return response.ToString();
    }

    /// <summary>流式对话</summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(string message)
    {
        _session ??= await _agent.CreateSessionAsync();
        await foreach (var update in _agent.RunStreamingAsync(message, _session))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    public void ResetSession() => _session = null;
}
