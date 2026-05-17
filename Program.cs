using System.Reflection;
using Microsoft.OpenApi.Models;
using llm_agent_demo.AgentWorkflow;
using llm_agent_demo.AgentWorkflow.AgentSteps.Cognitive;
using llm_agent_demo.AgentWorkflow.AgentSteps.PostProcessing;
using llm_agent_demo.Services.LLM;

// ==========================================================================
// 加载 .env 文件（模拟 Node.js / Python 的 dotenv 行为）
// .NET 默认不读取 .env 文件，这里手动解析并将值注入到系统环境变量中。
// 注意：仅在当前 Key 尚未设置时覆盖，已存在的 OS 环境变量优先。
// ==========================================================================
LoadEnvFile(".env");

var builder = WebApplication.CreateBuilder(args);

// ==========================================================================
// 注册服务（DI — 依赖注入）
// .NET 的 DI 容器管理所有服务的生命周期，通过构造函数自动注入。
// ==========================================================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 缩进格式化 JSON 输出（方便控制台调试）
        options.JsonSerializerOptions.WriteIndented = true;
        // 属性名保持 camelCase（与前端约定一致）
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
// ---- 注册 Swagger（在线 API 调试页面）----
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LLM Agent Demo API",
        Version = "v1",
        Description = "基于 .NET 10 的 LLM 调用与智能体工作流 API。支持 DeepSeek、Ollama 等多种 LLM 提供方。"
    });

    // 加载控制器上的 XML 注释，让 Swagger 页面展示接口说明
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ---- 注册 HttpClient（用于调用 LLM API）----
builder.Services.AddHttpClient();

// ==========================================================================
// LLM API Key 读取策略（优先级从高到低）：
//   1. 环境变量 LLMCONFIG__DEEPSEEK__APIKEY（Docker 部署推荐）
//   2. 环境变量 DEEPSEEK_API_KEY（简便写法）
//   3. User Secrets（本地开发推荐）：dotnet user-secrets set "LlmConfig:DeepSeek:ApiKey" "sk-xxx"
//   4. appsettings.json 中的 ApiKey 字段（公开仓库中为空占位）
//
// .NET 的 IConfiguration 自动合并以下来源（后者覆盖前者）：
//   appsettings.json → appsettings.Development.json → User Secrets → 环境变量 → 命令行
// ==========================================================================

// 解析 DeepSeek API Key：优先环境变量 → User Secrets → appsettings.json
var deepSeekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                  ?? builder.Configuration["LlmConfig:DeepSeek:ApiKey"]
                  ?? "";

if (string.IsNullOrEmpty(deepSeekApiKey))
{
    Console.WriteLine("⚠ 警告: DeepSeek API Key 未配置。");
    Console.WriteLine("  请通过以下方式之一设置：");
    Console.WriteLine("  1. 环境变量: export DEEPSEEK_API_KEY=sk-xxx");
    Console.WriteLine("  2. User Secrets: dotnet user-secrets set \"LlmConfig:DeepSeek:ApiKey\" \"sk-xxx\"");
    Console.WriteLine("  3. appsettings.json 中 LlmConfig.DeepSeek.ApiKey 字段");
}

// 解析 Ollama BaseUrl（同样的优先级）
var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
                 ?? builder.Configuration["LlmConfig:Ollama:BaseUrl"]
                 ?? "http://localhost:11434";

// ---- 注册 DeepSeek 服务 ----
builder.Services.AddHttpClient<DeepSeekService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddSingleton(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DeepSeekService));
    return new DeepSeekService(httpClient, deepSeekApiKey);
});

// ---- 注册 Ollama 服务 ----
builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OllamaService));
    return new OllamaService(httpClient, ollamaBaseUrl);
});

// ---- 接口多实现注册 ----
builder.Services.AddSingleton<ILLMService>(sp => sp.GetRequiredService<DeepSeekService>());
builder.Services.AddSingleton<ILLMService>(sp => sp.GetRequiredService<OllamaService>());
builder.Services.AddSingleton<LLMServiceFactory>();

// ---- 注册智能体工作流步骤 ----
builder.Services.AddSingleton<IAgentStep, IntentAnalysisStep>(sp =>
{
    var llm = sp.GetRequiredService<DeepSeekService>();
    return new IntentAnalysisStep(llm);
});
builder.Services.AddSingleton<IAgentStep, GenerationStep>(sp =>
{
    var llm = sp.GetRequiredService<DeepSeekService>();
    return new GenerationStep(llm);
});
builder.Services.AddSingleton<IAgentStep, ValidateOutputStep>(sp =>
{
    var llm = sp.GetRequiredService<DeepSeekService>();
    return new ValidateOutputStep(llm);
});

// ---- RAG 预处理 Agent（已激活 — 用于意图提取 + 查询分解）----
builder.Services.AddSingleton<RAGProcessingAgent>(sp =>
{
    var llm = sp.GetRequiredService<DeepSeekService>();
    return new RAGProcessingAgent(llm);
});
// 同时注册为 IAgentStep（可被 SimpleAgentWorkflow 自动编排）
builder.Services.AddSingleton<IAgentStep>(sp => sp.GetRequiredService<RAGProcessingAgent>());

// ---- 可选 Step（按需取消注释以扩展工作流）----
// builder.Services.AddSingleton<IAgentStep, PlanningStep>(sp => new PlanningStep(sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, ReasoningStep>(sp => new ReasoningStep(sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, SummarizationStep>(sp => new SummarizationStep(sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, SelfCritiqueStep>(sp => new SelfCritiqueStep(sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, GuardrailStep>(sp => new GuardrailStep(sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, FallbackStep>(sp => new FallbackStep(sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, FormatOutputStep>(sp => new FormatOutputStep(sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, WebSearchStep>();
// builder.Services.AddSingleton<IAgentStep, DatabaseQueryStep>();
// builder.Services.AddSingleton<IAgentStep, RouterStep>(sp => new RouterStep(
//     new Dictionary<string, string> { ["问答"] = "GenerationStep", ["翻译"] = "GenerationStep", ["搜索"] = "WebSearchStep" }));
// builder.Services.AddSingleton<IAgentStep, LoopConditionStep>(sp => new LoopConditionStep(maxLoops: 5, sp.GetRequiredService<DeepSeekService>()));
// builder.Services.AddSingleton<IAgentStep, MergeStep>(sp => new MergeStep(sp.GetRequiredService<DeepSeekService>()));

// ---- 注册工作流编排器 ----
builder.Services.AddSingleton<SimpleAgentWorkflow>();

// ---- 添加 CORS（方便前端调试）----
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Swagger UI — 在线 API 调试页面
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "LLM Agent Demo API v1");
    options.RoutePrefix = "swagger"; // 访问路径: http://localhost:5004/swagger
});

app.UseCors();
app.MapControllers();
app.Run();

// ==========================================================================
// .env 文件加载器 — 轻量实现，无需额外 NuGet 包
// ==========================================================================
static void LoadEnvFile(string filePath)
{
    if (!File.Exists(filePath))
    {
        Console.WriteLine($"[dotenv] 未找到 {filePath} 文件，跳过。可运行: cp .env.example .env");
        return;
    }

    foreach (var line in File.ReadAllLines(filePath))
    {
        var trimmed = line.Trim();

        // 跳过空行和注释
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            continue;

        // 查找第一个 =（允许 value 中包含 =）
        var eqIndex = trimmed.IndexOf('=');
        if (eqIndex < 0)
            continue;

        var key = trimmed[..eqIndex].Trim();
        var value = trimmed[(eqIndex + 1)..].Trim();

        // 移除可选的双引号包裹
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            value = value[1..^1];

        // 仅在环境变量尚未设置时写入（OS 环境变量优先于 .env）
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
            // 不打印 API Key 值，仅打印 key 名
            var displayValue = key.Contains("KEY", StringComparison.OrdinalIgnoreCase)
                ? "***（已隐藏）"
                : value;
            Console.WriteLine($"[dotenv] {key}={displayValue}");
        }
    }
}
