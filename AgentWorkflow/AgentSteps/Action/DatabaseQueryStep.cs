using llm_agent_demo.Services.LLM;

namespace llm_agent_demo.AgentWorkflow.AgentSteps.Action;

/// <summary>
/// 行动类 — 数据库查询。
///
/// 根据上下文中指定的 SQL 语句执行数据库查询。
/// 当前为骨架实现，需替换为真实数据库连接。
///
/// 集成方向：
///   - Entity Framework Core（SQL Server / PostgreSQL / MySQL）
///   - Dapper（轻量 ORM）
///   - 向量数据库（Milvus / Qdrant / Weaviate）用于 RAG 检索
///
/// 输入上下文 key：  "sql" 或 "query"（查询语句）
/// 输出上下文 key：  "query_result"（JSON 字符串）
/// </summary>
public class DatabaseQueryStep : IAgentStep
{
    // 集成真实数据库时：
    // private readonly IDbConnection _db;
    // public DatabaseQueryStep(IDbConnection db) { _db = db; }

    public string Name => "数据库查询";

    public async Task<Dictionary<string, object>> ExecuteAsync(
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default)
    {
        var sql = context.GetValueOrDefault("sql")?.ToString()
               ?? context.GetValueOrDefault("query")?.ToString()
               ?? "";

        // ==================================================================
        // 骨架占位 — 实际使用时替换为真实数据库查询。
        //
        // Dapper 示例：
        //   var rows = await _db.QueryAsync(sql);
        //   context["query_result"] = JsonSerializer.Serialize(rows);
        //
        // EF Core 示例：
        //   var data = await _context.YourTable.FromSqlRaw(sql).ToListAsync();
        //   context["query_result"] = JsonSerializer.Serialize(data);
        // ==================================================================

        context["query_result"] = $"[骨架占位] SQL 已接收: {sql[..Math.Min(sql.Length, 100)]}... (集成真实 DB 后返回查询结果)";

        await Task.Delay(100, cancellationToken);
        return context;
    }
}
