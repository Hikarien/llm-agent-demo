using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;

namespace llm_agent_demo.AgentWorkflow.MSAgents;

/// <summary>
/// 多 Agent 技术评审 — 演示 MAF 的多 Agent 并行协作。
///
/// 架构师 + 安全专家 并行审查 → Orchestrator 汇总。
/// 每个 Agent 是独立的 ChatClient.AsAIAgent() 实例，互不干扰。
/// </summary>
public class MultiAgentReviewer
{
    private readonly string _apiKey;

    public MultiAgentReviewer(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task<ReviewResult> ReviewAsync(string proposal, CancellationToken ct = default)
    {
        var archTask = AskExpertAsync("架构师",
            "你是资深架构师。从架构设计、可扩展性角度分析方案，给出 2~3 点意见。",
            proposal, ct);
        var secTask = AskExpertAsync("安全专家",
            "你是安全专家。从安全性、数据保护角度审查方案，指出 2~3 个风险。",
            proposal, ct);

        await Task.WhenAll(archTask, secTask);

        var summary = await SummarizeAsync(proposal, archTask.Result, secTask.Result, ct);

        return new ReviewResult
        {
            ArchitectureOpinion = archTask.Result,
            SecurityOpinion = secTask.Result,
            Summary = summary
        };
    }

    private async Task<string> AskExpertAsync(
        string role, string instructions, string question, CancellationToken ct)
    {
        var agent = CreateAgent(instructions, role);
        return (await agent.RunAsync(question, cancellationToken: ct)).ToString();
    }

    private async Task<string> SummarizeAsync(
        string proposal, string arch, string sec, CancellationToken ct)
    {
        var agent = CreateAgent("你是技术决策者。根据专家意见生成 150 字评审报告。", "Orchestrator");
        return (await agent.RunAsync(
            $"方案：{proposal}\n\n架构师：{arch}\n\n安全专家：{sec}\n\n生成报告。",
            cancellationToken: ct)).ToString();
    }

    private AIAgent CreateAgent(string instructions, string name)
    {
        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(_apiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri("https://api.deepseek.com/v1")
            });
        return client.GetChatClient("deepseek-chat").AsAIAgent(instructions, name);
    }
}

public class ReviewResult
{
    public string ArchitectureOpinion { get; set; } = "";
    public string SecurityOpinion { get; set; } = "";
    public string Summary { get; set; } = "";
}
