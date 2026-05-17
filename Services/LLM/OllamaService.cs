using System.Text;
using System.Text.Json;
using llm_agent_demo.Models;

namespace llm_agent_demo.Services.LLM;

/// <summary>
/// Ollama 本地 LLM 服务实现。
/// 调用本地运行的 Ollama 服务（默认 http://localhost:11434/api/chat）。
///
/// 安装 Ollama：brew install ollama (macOS) 或访问 https://ollama.com
/// 拉取模型：   ollama pull qwen2.5:7b
/// 启动服务：   ollama serve （默认后台运行）
///
/// Ollama API 格式与 OpenAI 有所不同：
///   - 端点：POST /api/chat
///   - 不需要 Authorization 头
///   - 流式数据每行一个 JSON（非 SSE 的 data: 前缀格式）
/// </summary>
public class OllamaService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OllamaService(HttpClient httpClient, string baseUrl = "http://localhost:11434")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <inheritdoc />
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        // 将 ChatRequest 转换为 Ollama 格式
        var ollamaRequest = new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false,
            options = new
            {
                temperature = request.Temperature,
                num_predict = request.MaxTokens
            }
        };

        var json = JsonSerializer.Serialize(ollamaRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        // Ollama 返回格式与 OpenAI 不同，手动映射
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        var msg = root.GetProperty("message");

        return new ChatResponse
        {
            Id = Guid.NewGuid().ToString(),
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = request.Model,
            Choices = new List<ChatChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage
                    {
                        Role = msg.GetProperty("role").GetString() ?? "assistant",
                        Content = msg.GetProperty("content").GetString() ?? ""
                    },
                    FinishReason = root.TryGetProperty("done_reason", out var reason)
                        ? reason.GetString() ?? "stop"
                        : "stop"
                }
            },
            Usage = root.TryGetProperty("eval_count", out var evalCount)
                ? new TokenUsage
                {
                    PromptTokens = root.TryGetProperty("prompt_eval_count", out var p) ? p.GetInt32() : 0,
                    CompletionTokens = evalCount.GetInt32(),
                    TotalTokens = (root.TryGetProperty("prompt_eval_count", out var pe) ? pe.GetInt32() : 0) + evalCount.GetInt32()
                }
                : null
        };
    }

    /// <inheritdoc />
    public async Task<string> ChatStreamAsync(ChatRequest request, Action<string> onChunk, CancellationToken cancellationToken = default)
    {
        var ollamaRequest = new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            options = new
            {
                temperature = request.Temperature,
                num_predict = request.MaxTokens
            }
        };

        var json = JsonSerializer.Serialize(ollamaRequest);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", httpContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fullText = new StringBuilder();

        // Ollama 流式：每行一个完整 JSON 对象（非 SSE 的 data: 前缀）
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                var text = content.GetString() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    fullText.Append(text);
                    onChunk(text);
                }
            }
        }

        return fullText.ToString();
    }
}
