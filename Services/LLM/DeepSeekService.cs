using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using llm_agent_demo.Models;

namespace llm_agent_demo.Services.LLM;

/// <summary>
/// DeepSeek LLM 服务实现。
/// DeepSeek API 与 OpenAI API 完全兼容，Base URL 为 https://api.deepseek.com。
///
/// 使用方式：
///   1. 构造函数注入 HttpClientFactory，由框架管理连接池
///   2. 每次调用时在请求头带上 Bearer Token
///   3. 调用 /v1/chat/completions 端点
/// </summary>
public class DeepSeekService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    // DeepSeek API 基地址
    private const string BaseUrl = "https://api.deepseek.com";

    /// <summary>
    /// 构造函数 — API Key 由 appsettings.json 注入
    /// </summary>
    public DeepSeekService(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        // 提前校验 — 避免发出一个注定 401 的请求
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "DeepSeek API Key 未配置。请通过以下方式之一设置：\n" +
                "  1. 环境变量: export DEEPSEEK_API_KEY=sk-xxx\n" +
                "  2. User Secrets: dotnet user-secrets set \"LlmConfig:DeepSeek:ApiKey\" \"sk-xxx\"\n" +
                "  3. 直接编辑 appsettings.json 中的 LlmConfig.DeepSeek.ApiKey（注意不要提交到 Git）");
        }

        request.Stream = false;
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        // DeepSeek 要求 Accept 头
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ChatResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result ?? new ChatResponse();
    }

    /// <inheritdoc />
    public async Task<string> ChatStreamAsync(ChatRequest request, Action<string> onChunk, CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        // 发送请求，获取流式响应
        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fullText = new StringBuilder();

        // 逐行读取 SSE 流
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..]; // 去掉 "data: " 前缀
            if (data == "[DONE]") break;

            var chunk = JsonSerializer.Deserialize<StreamChunk>(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(content))
            {
                fullText.Append(content);
                onChunk(content); // 触发回调，让调用方实时收到文本
            }
        }

        return fullText.ToString();
    }
}
