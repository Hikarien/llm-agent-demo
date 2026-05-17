namespace llm_agent_demo.AgentWorkflow;

/// <summary>
/// 智能体步骤接口 — 工作流中每个步骤都必须实现此接口。
///
/// 设计思路：
///   一个"智能体工作流"本质上就是多个步骤的串联/并联。
///   每个步骤接收一个上下文（字典），处理后更新上下文，
///   下一步骤可以从前面的步骤中读取结果。
///
/// 类比：LangChain 的 Chain、Semantic Kernel 的 Function。
///
/// 如何自定义步骤：
///   1. 新建类，实现 IAgentStep
///   2. 重写 Name（步骤名）和 ExecuteAsync（核心逻辑）
///   3. 在 SimpleAgentWorkflow 中注册
/// </summary>
public interface IAgentStep
{
    /// <summary>步骤名称 — 用于日志和调试输出</summary>
    string Name { get; }

    /// <summary>
    /// 执行步骤核心逻辑
    /// </summary>
    /// <param name="context">
    ///   共享上下文 — 一个字典，key 为字符串，value 为 object。
    ///   约定 key：
    ///     "user_input"   → 用户原始输入 (string)
    ///     "intent"       → 意图分析结果 (string)
    ///     "draft"        → 生成的草稿 (string)
    ///     "final_output" → 最终输出 (string)
    /// </param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行后的上下文（可以与输入是同一个对象）</returns>
    Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default);
}
