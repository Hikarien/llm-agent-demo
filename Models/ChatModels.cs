using System.Text.Json.Serialization;

namespace llm_agent_demo.Models;

/// <summary>
/// 聊天请求模型 — 对应 OpenAI/DeepSeek 的 /v1/chat/completions 接口
/// </summary>
public class ChatRequest
{
    /// <summary>使用的模型名称，如 deepseek-chat、qwen2.5:7b</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "deepseek-chat";

    /// <summary>对话消息列表</summary>
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>温度参数 0~2，越高越随机</summary>
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;

    /// <summary>最大生成 token 数</summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 2048;

    /// <summary>是否流式返回（SSE）</summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

/// <summary>
/// 单条消息
/// </summary>
public class ChatMessage
{
    /// <summary>角色：system / user / assistant / tool</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    /// <summary>消息内容</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    /// <summary>工具调用 ID（仅 role=tool 时需要）</summary>
    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }

    /// <summary>可选的名称</summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}

/// <summary>
/// 聊天响应模型 — 非流式
/// </summary>
public class ChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("object")]
    public string Object { get; set; } = "";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public TokenUsage? Usage { get; set; }
}

public class ChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();

    /// <summary>结束原因：stop / length / tool_calls</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class TokenUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// 流式响应片段（SSE 中 data 字段的 JSON 内容）
/// </summary>
public class StreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("object")]
    public string Object { get; set; } = "";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("choices")]
    public List<StreamChoice> Choices { get; set; } = new();
}

public class StreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>delta 而非 message — 流式每次只推送增量内容</summary>
    [JsonPropertyName("delta")]
    public StreamDelta Delta { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class StreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
