# LLM Agent Demo — .NET 10 WebAPI 智能体开发入门框架

基于 **.NET 10 / ASP.NET Core WebAPI** 的 LLM 调用与 Agent 工作流示例项目。为学习 .NET 智能体开发提供一个可运行的起点。

---

## 目录

1. [项目结构](#1-项目结构)
2. [快速开始](#2-快速开始)
3. [配置 API Key](#3-配置-api-key)
4. [Docker 部署](#4-docker-部署)
5. [API 端点](#5-api-端点)
6. [核心架构](#6-核心架构)
7. [Agent Steps 体系](#7-agent-steps-体系)
8. [上下文 Key 约定](#8-上下文-key-约定)
9. [工作流组合示例](#9-工作流组合示例)
10. [扩展指南](#10-扩展指南)
11. [技术栈](#11-技术栈)

---

## 1. 项目结构

```
llm-agent-demo/
├── Program.cs                              # 入口点 + DI 注册中心
├── appsettings.json                        # 配置（LLM API Key、Ollama 地址、日志）
├── README.md                               # 本文档
│
├── Controllers/
│   └── LLMController.cs                    # API 控制器（4 个端点）
│
├── Models/
│   ├── ChatModels.cs                       # 聊天相关：请求/响应/消息/流式块/Token 用量
│   └── AgentModels.cs                      # 工作流相关：请求/响应/步骤记录
│
├── Services/LLM/                           # ── LLM 服务抽象层 ──
│   ├── ILLMService.cs                      #   统一接口（ChatAsync + ChatStreamAsync）
│   ├── DeepSeekService.cs                  #   DeepSeek 实现（OpenAI 兼容 /v1/chat/completions）
│   ├── OllamaService.cs                    #   Ollama 实现（本地 /api/chat）
│   └── LLMServiceFactory.cs                #   工厂（按名称字符串切换提供方）
│
├── AgentWorkflow/                          # ── Agent 工作流层 ──
│   ├── IAgentStep.cs                       #   步骤接口（所有 Step 的契约）
│   ├── SimpleAgentWorkflow.cs              #   编排器（线性串联 + debug 记录）
│   └── AgentSteps/
│       ├── Cognitive/                      #   认知类（思考 — 不产生外部副作用）
│       │   ├── IntentAnalysisStep.cs       #     意图分析
│       │   ├── GenerationStep.cs           #     生成回复
│       │   ├── PlanningStep.cs             #     任务规划（拆解子任务）
│       │   ├── ReasoningStep.cs            #     推理（事实 → 结论）
│       │   ├── SummarizationStep.cs        #     摘要（压缩长文本）
│       │   └── SelfCritiqueStep.cs         #     自我反思（评价自己的输出）
│       ├── Action/                         #   行动类（执行 — 产生外部副作用）
│       │   ├── WebSearchStep.cs            #     联网搜索（骨架）
│       │   ├── ToolCallStep.cs             #     工具调用（注册任意函数分发）
│       │   └── DatabaseQueryStep.cs        #     数据库查询（骨架）
│       ├── Orchestration/                  #   流程控制类（编排 — 决定执行路径）
│       │   ├── RouterStep.cs               #     路由（静态 + LLM 动态路由）
│       │   ├── LoopConditionStep.cs        #     循环条件（ReAct 使用）
│       │   └── MergeStep.cs                #     合并（多分支结果汇总）
│       └── PostProcessing/                 #   后处理类（格式 & 安全 — 最终打磨）
│           ├── ValidateOutputStep.cs       #     验证输出（自检）
│           ├── FormatOutputStep.cs         #     格式化（Markdown / JSON / Plain）
│           ├── GuardrailStep.cs            #     安全护栏（内容审核）
│           └── FallbackStep.cs             #     兜底回复（异常友好降级）
└── Properties/
    └── launchSettings.json
```

**分层关系：**

```
  Controller  →  Factory  →  ILLMService  →  DeepSeek / Ollama API
      │                                  (HTTP 调用外部 LLM)
      │
      └──────  SimpleAgentWorkflow  ──────┐
                     │                    │
            IAgentStep  ←  IAgentStep  ←  ...
            (Step 1)        (Step 2)      (Step N)
                     │
            Dictionary<string, object> context
                   (共享上下文)
```

---

## 2. 快速开始

### 2.1 前置条件

- .NET 10 SDK（`dotnet --version` → 10.0.x）
- DeepSeek API Key（从 https://platform.deepseek.com 获取）
- （可选）Ollama 本地运行

### 2.2 启动

```bash
cd demo/llm-agent-demo

# 方式 1：通过环境变量传入 API Key
export DEEPSEEK_API_KEY=sk-your-key-here
dotnet run

# 方式 2：通过 User Secrets（不会被提交到 Git）
dotnet user-secrets set "LlmConfig:DeepSeek:ApiKey" "sk-your-key-here"
dotnet run
# → http://localhost:5004
```

> 详细配置说明见 [第 3 节：配置 API Key](#3-配置-api-key)。

### 2.3 验证可用提供方

```bash
curl http://localhost:5004/api/llm/providers
# → {"providers":["DeepSeek","Ollama"]}
```

### 2.4 非流式聊天

```bash
curl -X POST "http://localhost:5004/api/llm/chat?provider=DeepSeek" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "deepseek-chat",
    "messages": [
      {"role": "user", "content": "用三句话介绍.NET 10"}
    ],
    "temperature": 0.7
  }'
```

### 2.5 流式聊天（SSE）

```bash
curl -N -X POST "http://localhost:5004/api/llm/chat/stream?provider=DeepSeek" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "deepseek-chat",
    "messages": [{"role": "user", "content": "写一首关于编程的七言绝句"}]
  }'
```

### 2.6 执行 Agent 工作流

```bash
curl -X POST http://localhost:5004/api/llm/agent/workflow \
  -H "Content-Type: application/json" \
  -d '{
    "userInput": "请用通俗的语言解释什么是量子纠缠",
    "provider": "DeepSeek",
    "model": "deepseek-chat",
    "debug": true
  }'
```

响应示例（debug=true）：

```json
{
  "finalOutput": "量子纠缠可以理解为...",
  "elapsedMs": 4521,
  "steps": [
    {"stepName": "意图分析", "elapsedMs": 823, "inputSummary": "...", "outputSummary": "..."},
    {"stepName": "生成回复", "elapsedMs": 2812, "inputSummary": "...", "outputSummary": "..."},
    {"stepName": "验证输出", "elapsedMs": 886, "inputSummary": "...", "outputSummary": "..."}
  ]
}
```

### 2.7 使用 Ollama 本地模型

```bash
brew install ollama                 # 安装
ollama pull qwen2.5:7b              # 拉取模型
ollama serve                        # 启动（端口 11434）

# 测试
curl -X POST "http://localhost:5004/api/llm/chat?provider=Ollama" \
  -H "Content-Type: application/json" \
  -d '{"model":"qwen2.5:7b","messages":[{"role":"user","content":"你好"}]}'
```

---

## 3. 配置 API Key

> **重要：API Key 绝不能直接写在 `appsettings.json` 中提交到 Git。**
> 本项目提供了三种安全的配置方式，按优先级从高到低：

| 优先级 | 方式 | 适用场景 | 示例命令 |
|--------|------|---------|---------|
| 1（最高）| 环境变量 `DEEPSEEK_API_KEY` | Docker 部署、CI/CD | `export DEEPSEEK_API_KEY=sk-xxx` |
| 2 | `.env` 文件 | 本地开发（最方便） | `cp .env.example .env`，编辑填入 Key |
| 3 | .NET User Secrets | 本地开发（微软官方推荐）| `dotnet user-secrets set "LlmConfig:DeepSeek:ApiKey" "sk-xxx"` |
| 4（最低）| 修改 `appsettings.json` | 临时调试（注意不要提交）| 直接编辑 `ApiKey` 字段 |

### 3.1 本地开发：.env 文件（推荐）

```bash
cd demo/llm-agent-demo

# 复制模板文件
cp .env.example .env

# 编辑 .env，填入真实 Key
# DEEPSEEK_API_KEY=sk-your-api-key-here

# 直接启动即可，Program.cs 启动时自动加载 .env
dotnet run
```

`.env` 文件已被 `.gitignore` 忽略，不会被提交到 Git。程序启动时控制台会打印加载日志（Key 值自动隐藏）。

### 3.2 本地开发：User Secrets（备选）

```bash
cd demo/llm-agent-demo

dotnet user-secrets init
dotnet user-secrets set "LlmConfig:DeepSeek:ApiKey" "sk-your-api-key-here"
dotnet run
```

User Secrets 存储在 `~/.microsoft/usersecrets/<guid>/secrets.json`，不会出现在项目目录中。

### 3.3 环境变量（Docker / 生产环境）

```bash
# Linux / macOS
export DEEPSEEK_API_KEY=sk-your-api-key-here

# 持久化：写入 shell 配置文件
echo 'export DEEPSEEK_API_KEY=sk-your-api-key-here' >> ~/.zshrc

# 或使用 .env 文件（已被 .gitignore 忽略）
cp .env.example .env
# 编辑 .env 填入真实 Key，通过 dotenv 工具或 IDE 加载
```

### 3.4 启动时无 Key 的提示

如果未配置 API Key，程序仍可启动，但调用 DeepSeek 时会失败。控制台会输出：

```
⚠ 警告: DeepSeek API Key 未配置。
  请通过以下方式之一设置：
  1. 环境变量: export DEEPSEEK_API_KEY=sk-xxx
  2. User Secrets: dotnet user-secrets set "LlmConfig:DeepSeek:ApiKey" "sk-xxx"
  3. appsettings.json 中 LlmConfig.DeepSeek.ApiKey 字段
```

---

## 4. Docker 部署

项目根目录提供了 `Dockerfile` 和 `.dockerignore`，可直接构建和运行。

### 4.1 构建镜像

```bash
cd demo/llm-agent-demo
docker build -t llm-agent-demo:latest .
```

### 4.2 运行容器

```bash
# 传入 API Key（推荐）
docker run -d \
  -e DEEPSEEK_API_KEY=sk-your-key-here \
  -p 5004:8080 \
  --name llm-agent-demo \
  llm-agent-demo:latest

# 如果需要连接本机 Ollama，使用 host 网络模式
docker run -d \
  -e DEEPSEEK_API_KEY=sk-your-key-here \
  -e OLLAMA_BASE_URL=http://host.docker.internal:11434 \
  -p 5004:8080 \
  --name llm-agent-demo \
  llm-agent-demo:latest
```

> **注意**：容器内 ASP.NET 监听 `8080`（.NET 8+ 镜像默认），通过 `-p 5004:8080` 映射到宿主机。

### 4.3 Dockerfile 说明

多阶段构建，分为两层：

| 阶段 | 基础镜像 | 用途 |
|------|---------|------|
| Stage 1 — build | `mcr.microsoft.com/dotnet/sdk:10.0` | restore + publish |
| Stage 2 — runtime | `mcr.microsoft.com/dotnet/aspnet:10.0` | 运行（含非 root 用户） |

最终镜像仅包含运行时依赖和发布产物，不含 SDK，体积小且安全。

---

## 5. API 端点

| 方法 | 端点 | 说明 | Query 参数 |
|------|------|------|-----------|
| GET | `/api/llm/providers` | 列出可用的 LLM 提供方 | — |
| POST | `/api/llm/chat` | 非流式聊天 | `provider` (DeepSeek/Ollama) |
| POST | `/api/llm/chat/stream` | 流式聊天（SSE） | `provider` |
| POST | `/api/llm/agent/workflow` | 执行 Agent 工作流 | — (via body) |

**工作流请求体：**

```jsonc
{
  "userInput": "...",        // 必填 — 用户输入
  "provider": "DeepSeek",    // 必填 — LLM 提供方
  "model": "deepseek-chat",  // 必填 — 模型名
  "debug": true              // 可选 — 是否展示每步耗时和上下文
}
```

---

## 6. 核心架构

### 6.1 分层架构

```
┌──────────────────────────────────────────────┐
│  API 层 (Controller)                         │  ← HTTP 入口
├──────────────────────────────────────────────┤
│  编排层 (SimpleAgentWorkflow)                 │  ← 步骤串联 + 上下文管理
├──────────────┬───────────────────────────────┤
│ Agent Steps  │   服务层                       │
│              │   ┌─────────────────────────┐ │
│ Cognitive    │   │ LLMServiceFactory       │ │  ← 按名称切换
│ Action       │   │   ├── DeepSeekService   │ │
│ Orchestration│   │   └── OllamaService     │ │
│ PostProcess  │   │   (均实现 ILLMService)   │ │
│              │   └─────────────────────────┘ │
├──────────────┴───────────────────────────────┤
│  模型层 (Models)                              │  ← ChatRequest/Response, AgentModels
└──────────────────────────────────────────────┘
```

### 6.2 LLM 服务层 — 面向接口

```
                    ┌──────────────┐
                    │  ILLMService │  ← 统一契约
                    │ ChatAsync()  │
                    │ ChatStreamAsync()
                    └──────┬───────┘
              ┌────────────┴────────────┐
              │                         │
    ┌─────────▼────────┐    ┌──────────▼─────────┐
    │ DeepSeekService  │    │   OllamaService    │
    │ /v1/chat/        │    │   /api/chat        │
    │ completions      │    │   (本地协议)        │
    │ (OpenAI 兼容)    │    │                    │
    └──────────────────┘    └────────────────────┘
```

**添加新提供方的步骤：**
1. 新建类实现 `ILLMService`
2. 在 `Program.cs` 中 `builder.Services.AddSingleton<ILLMService, 你的类>()` 注册
3. `LLMServiceFactory` 自动发现（通过 `GetType().Name` 推断名称）

### 6.3 智能体工作流 — 上下文传递

```
  用户输入 "user_input"
        │
        ▼
┌───────────────────┐
│  IntentAnalysis   │  → context["intent"] = "..."
└───────┬───────────┘
        │
        ▼
┌───────────────────┐
│  Generation       │  → context["draft"] = "..."    使用 context["intent"]
└───────┬───────────┘
        │
        ▼
┌───────────────────┐
│  ValidateOutput   │  → context["final_output"] = "..."  使用 context["draft"]
└───────┬───────────┘
        │
        ▼
  返回给用户
```

每个 Step 从 `Dictionary<string, object> context` 读取需要的 key，处理后写入新的 key。下一步可以读取前一步的输出。

Key 命名是**约定而非强制**，只要 Step 之间对齐即可。本项目使用一套标准约定（见第 8 节）。

### 6.4 DI 依赖注入

```
注册（Program.cs）:
  builder.Services.AddSingleton<ILLMService, DeepSeekService>();

消费（任意类）:
  public class MyController(ILLMService service) { ... }
```

.NET DI 支持三种生命周期：
- **Singleton** — 全局唯一实例（本项目大部分服务使用此模式）
- **Scoped** — 每个 HTTP 请求一个实例
- **Transient** — 每次注入创建新实例

---

## 7. Agent Steps 体系

所有 Step 都实现 `IAgentStep` 接口：

```csharp
public interface IAgentStep
{
    string Name { get; }
    Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken);
}
```

### 7.1 认知类（Cognitive）— "思考"

无外部副作用，只改变上下文内容。最常用的一类。

| Step | 输入 Key | 输出 Key | 说明 |
|------|---------|---------|------|
| **IntentAnalysisStep** | `user_input` | `intent` | 识别用户意图和情绪倾向 |
| **GenerationStep** | `user_input`, `intent` | `draft` | 生成回复草稿 |
| **PlanningStep** | `goal` → `user_input` | `plan` | 将复杂目标拆解为 JSON 数组子任务 |
| **ReasoningStep** | `question`, `facts` | `conclusion` | 基于给定事实做逻辑推理 |
| **SummarizationStep** | `long_text` → `user_input` | `summary` | 将长文本压缩为摘要 |
| **SelfCritiqueStep** | `draft` → `final_output` | `critique` | LLM 自评，四个维度打分 |

### 7.2 行动类（Action）— "执行"

产生外部副作用：调用 API、查数据库、执行代码。

| Step | 输入 Key | 输出 Key | 说明 |
|------|---------|---------|------|
| **WebSearchStep** | `search_query` → `user_input` | `search_results` | 联网搜索（骨架，预留 API 集成注释） |
| **ToolCallStep** | `tool_name`, `tool_args` | `tool_result` | 通用工具分发器，可注册任意函数 |
| **DatabaseQueryStep** | `sql` → `query` | `query_result` | 数据库查询（骨架，预留 EF/Dapper 注释） |

**ToolCallStep 用法：**

```csharp
var toolStep = new ToolCallStep();
toolStep.RegisterTool("get_weather", async args =>
{
    var city = args["city"].ToString();
    return $"城市: {city}, 天气: 晴, 温度: 25°C";
});

// 在上下文中放 tool_name 和 tool_args，toolStep 自动分发
context["tool_name"] = "get_weather";
context["tool_args"] = new Dictionary<string, object> { ["city"] = "北京" };
```

### 7.3 流程控制类（Orchestration）— "编排"

不产生业务输出，只决定下一步往哪走。

| Step | 输入 Key | 输出 Key | 说明 |
|------|---------|---------|------|
| **RouterStep** | `intent` → `route_key` | `next_step`, `route_reason` | 静态路由表 + LLM 动态路由 |
| **LoopConditionStep** | `loop_state`, `loop_count` | `loop_action` (continue/break) | 用于 ReAct 循环终止判断 |
| **MergeStep** | `branch_results` | `merged_result` | 合并多个并行分支结果 |

### 7.4 后处理类（PostProcessing）— "打磨"

最终输出前的格式化和安全检查。

| Step | 输入 Key | 输出 Key | 说明 |
|------|---------|---------|------|
| **ValidateOutputStep** | `user_input`, `draft` | `final_output` | 自检内容质量，修正或标记无效 |
| **FormatOutputStep** | `raw_output` → `final_output`, `format` | `formatted_output` | 格式化为 Markdown/JSON/Plain |
| **GuardrailStep** | `content` → `final_output` | `guardrail_result`, `safe_content` | 安全护栏，拦截不当内容 |
| **FallbackStep** | `error`, `user_input` | `recovery_output` | 异常时生成友好道歉回复 |

---

## 8. 上下文 Key 约定

Step 之间的通信完全通过统一的一组 key。下面是没有特殊需要就不应偏离的标准命名：

```
user_input          — 用户原始输入（工作流起点）
intent              — LLM 分析的意图
plan                — PlanningStep 产出的子任务列表
question            — 需要推理的问题
facts               — 推理用的事实材料
long_text           — 待压缩的长文本
draft               — LLM 生成的回复草稿
summary             — 摘要结果
conclusion          — 推理结论
critique            — 自我反思的评审意见
search_query        — 搜索关键词
search_results      — 搜索返回结果
tool_name           — 要调用的工具名
tool_args           — 工具调用参数 (Dictionary)
tool_result         — 工具调用返回
sql / query         — 数据库查询语句
query_result        — 数据库返回结果
next_step           — RouterStep 输出的下一步名称
route_reason        — 路由决策原因
loop_state          — 当前循环状态描述
loop_count          — 已执行循环次数 (int)
loop_action         — "continue" / "break"
loop_reason         — 循环决策原因
branch_results      — 多个分支的结果集合
merged_result       — 合并后的统一结果
final_output        — 最终输出（ValidateOutput / Fallback 写入）
formatted_output    — 格式化后的输出
guardrail_result    — "pass" / "block"
guardrail_reason    — 拦截原因
safe_content        — 通过护栏的清洁内容
recovery_output     — FallbackStep 的兜底回复
format              — 目标格式：markdown / json / text
error               — 异常信息
content             — 待检查的通用内容
```

---

## 9. 工作流组合示例

### 9.1 默认工作流（已注册）

```
IntentAnalysisStep → GenerationStep → ValidateOutputStep
```

`program.cs` 中已注册，启动即可用。

### 9.2 带护栏的客服工作流

```
IntentAnalysisStep → GenerationStep → GuardrailStep → FormatOutputStep
```

在 `Program.cs` 中取消注释 `GuardrailStep` 和 `FormatOutputStep`，注释掉 `ValidateOutputStep`。

### 9.3 RAG 检索增强

```
IntentAnalysisStep → DatabaseQueryStep → MergeStep → GenerationStep → ValidateOutputStep
```

适合"先从知识库检索相关文档，然后让 LLM 基于文档回答"的场景。

### 9.4 ReAct 循环（思考 → 行动 → 观察）

```
PlanningStep → LoopConditionStep ──continue──→ ToolCallStep → ...
                 │
                 └──break──→ GenerationStep → ValidateOutputStep
```

`SimpleAgentWorkflow` 是线性链，改为循环模式需要将 `foreach` 改为 `while`，由 `LoopConditionStep` 控制终止。

### 9.5 路由分发

```
IntentAnalysisStep → RouterStep ──→ GenerationStep (问答类)
                                ├──→ WebSearchStep (搜索类)
                                └──→ ToolCallStep  (工具类)
```

在 `Program.cs` 中注册 `RouterStep` 时传入路由表：

```csharp
builder.Services.AddSingleton<IAgentStep, RouterStep>(sp => new RouterStep(
    new Dictionary<string, string> {
        ["问答"] = "GenerationStep",
        ["搜索"] = "WebSearchStep",
        ["执行"] = "ToolCallStep"
    },
    llmService: sp.GetRequiredService<DeepSeekService>()
));
```

---

## 10. 扩展指南

### 10.1 添加新的 LLM 提供方

1. 新建 `Services/LLM/MyProviderService.cs`，实现 `ILLMService`
2. 在 `Program.cs` 中注册：
   ```csharp
   builder.Services.AddSingleton<ILLMService, MyProviderService>();
   ```

### 10.2 添加新的 Agent Step

1. 在对应的子目录下新建 `.cs` 文件，实现 `IAgentStep`
2. 在 `Program.cs` 中注册：
   ```csharp
   // 简单 Step（不需要 LLM）
   builder.Services.AddSingleton<IAgentStep, MyStep>();
   
   // 需要 LLM 的 Step
   builder.Services.AddSingleton<IAgentStep, MyStep>(sp =>
       new MyStep(sp.GetRequiredService<DeepSeekService>()));
   ```

### 10.3 让 Step 使用不同 LLM

默认所有 Step 使用 DeepSeek。改为 Ollama：

```csharp
// Program.cs 中将 DeepSeekService 替换为 OllamaService
builder.Services.AddSingleton<IAgentStep, GenerationStep>(sp =>
{
    var llm = sp.GetRequiredService<OllamaService>();
    return new GenerationStep(llm);
});
```

### 10.4 实现复杂工作流

`SimpleAgentWorkflow` 是线性模式。做更复杂的编排：

```csharp
public class ReActWorkflow
{
    public async Task<WorkflowResponse> ExecuteAsync(string input, ...)
    {
        var context = new Dictionary<string, object> { ["user_input"] = input };
        
        while (true)
        {
            context = await thinkStep.ExecuteAsync(context);
            context = await actStep.ExecuteAsync(context);
            context = await observeStep.ExecuteAsync(context);
            context = await loopCondition.ExecuteAsync(context);
            
            if (context["loop_action"].ToString() == "break")
                break;
        }
        
        return new WorkflowResponse { FinalOutput = context["final_output"].ToString() };
    }
}
```

---

## 11. 技术栈

| 组件 | 版本 / 说明 |
|------|------------|
| .NET SDK | 10.0.203 |
| ASP.NET Core | 10.0 (WebAPI + Controllers) |
| 序列化 | System.Text.Json (内置) |
| HTTP | IHttpClientFactory (连接池管理) |
| 流式 | SSE (Server-Sent Events) |
| DI | 内置 Microsoft.Extensions.DependencyInjection |
| 日志 | ILogger (内置) |

**外部 LLM 依赖：**

| 提供方 | 端点 | 说明 |
|--------|------|------|
| DeepSeek | `https://api.deepseek.com/v1/chat/completions` | OpenAI 兼容，需 API Key |
| Ollama | `http://localhost:11434/api/chat` | 本地部署，无需 Key |
