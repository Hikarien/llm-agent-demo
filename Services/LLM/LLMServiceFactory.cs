namespace llm_agent_demo.Services.LLM;

/// <summary>
/// LLM 服务工厂 — 根据提供方名称创建对应的 ILLMService 实例。
///
/// 设计理由：
///   将"选择哪个 LLM"这个决策集中在一个地方，方便后续扩展
///   （如添加 OpenAI、文心一言、通义千问等），
///   调用方只需传一个 provider 字符串即可。
///
/// 使用方式：
///   var service = factory.Create("DeepSeek");  // 或 "Ollama"
///   var response = await service.ChatAsync(request);
/// </summary>
public class LLMServiceFactory
{
    private readonly Dictionary<string, ILLMService> _services;

    /// <summary>
    /// 构造函数 — 通过 DI 注入所有已注册的 ILLMService 实现
    /// </summary>
    public LLMServiceFactory(IEnumerable<ILLMService> services)
    {
        // 以服务类型的简写名称作为 key
        _services = new Dictionary<string, ILLMService>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in services)
        {
            var name = service.GetType().Name
                .Replace("Service", "")  // DeepSeekService → DeepSeek
                .Replace("LLM", "");     // 预留：防止命名冲突
            _services[name] = service;
        }
    }

    /// <summary>
    /// 根据提供方名称获取 LLM 服务实例
    /// </summary>
    /// <param name="provider">提供方名称，如 "DeepSeek" 或 "Ollama"（不区分大小写）</param>
    /// <returns>对应的 ILLMService 实例</returns>
    /// <exception cref="ArgumentException">提供方未注册时抛出</exception>
    public ILLMService Create(string provider)
    {
        if (_services.TryGetValue(provider, out var service))
            return service;

        var available = string.Join(", ", _services.Keys);
        throw new ArgumentException(
            $"未找到 LLM 提供方 '{provider}'。可用的提供方: {available}。" +
            $"请检查 appsettings.json 中的 LlmConfig。");
    }

    /// <summary>
    /// 获取所有已注册的提供方名称
    /// </summary>
    public IEnumerable<string> GetAvailableProviders() => _services.Keys;
}
