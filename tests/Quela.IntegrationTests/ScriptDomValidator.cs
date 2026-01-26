#if SQL_SERVER_CLIENT
using Microsoft.SqlServer.TransactSql.ScriptDom;
#endif

namespace Quela.IntegrationTests;

/// <summary>
/// Validates SQL using Microsoft's official SQL Server T-SQL parser (ScriptDom).
/// This provides real SQL Server syntax validation without needing a running instance.
/// </summary>
public static class ScriptDomValidator
{
#if SQL_SERVER_CLIENT
    private static readonly TSql160Parser Parser = new(initialQuotedIdentifiers: true);

    /// <summary>
    /// Parses SQL and returns any syntax errors.
    /// </summary>
    public static List<string> Validate(string sql)
    {
        using var reader = new StringReader(sql);
        Parser.Parse(reader, out var errors);
        return errors.Select(e => $"Line {e.Line}, Col {e.Column}: {e.Message}").ToList();
    }

    /// <summary>
    /// Validates SQL and throws if there are parse errors.
    /// </summary>
    public static void AssertValid(string sql)
    {
        var errors = Validate(sql);
        if (errors.Count > 0)
        {
            throw new Exception($"SQL Server parser errors:\n{string.Join("\n", errors)}\n\nSQL:\n{sql}");
        }
    }

    /// <summary>
    /// Parses SQL and returns the AST for inspection.
    /// </summary>
    public static TSqlFragment? Parse(string sql)
    {
        using var reader = new StringReader(sql);
        var fragment = Parser.Parse(reader, out var errors);
        return errors.Count == 0 ? fragment : null;
    }

    /// <summary>
    /// Validates that the SQL parses as a specific statement type.
    /// </summary>
    public static void AssertStatementType<TStatement>(string sql) where TStatement : TSqlStatement
    {
        using var reader = new StringReader(sql);
        var fragment = Parser.Parse(reader, out var errors);

        if (errors.Count > 0)
        {
            throw new Exception($"SQL Server parser errors:\n{string.Join("\n", errors.Select(e => e.Message))}\n\nSQL:\n{sql}");
        }

        if (fragment is not TSqlScript script || script.Batches.Count == 0)
        {
            throw new Exception($"Expected TSqlScript, got {fragment?.GetType().Name}");
        }

        var batch = script.Batches[0];
        if (batch.Statements.Count == 0)
        {
            throw new Exception("No statements found in SQL");
        }

        var statement = batch.Statements[0];
        if (statement is not TStatement)
        {
            throw new Exception($"Expected {typeof(TStatement).Name}, got {statement.GetType().Name}");
        }
    }

    /// <summary>
    /// Gets statement count in a batch.
    /// </summary>
    public static int GetStatementCount(string sql)
    {
        using var reader = new StringReader(sql);
        var fragment = Parser.Parse(reader, out _);

        if (fragment is TSqlScript script && script.Batches.Count > 0)
        {
            return script.Batches.Sum(b => b.Statements.Count);
        }
        return 0;
    }
#else
    /// <summary>
    /// Parses SQL and returns any syntax errors.
    /// Note: ScriptDom not available - returns empty list.
    /// </summary>
    public static List<string> Validate(string sql) => new();

    /// <summary>
    /// Validates SQL and throws if there are parse errors.
    /// Note: ScriptDom not available - skips validation.
    /// </summary>
    public static void AssertValid(string sql)
    {
        // ScriptDom not available - basic validation only
        if (string.IsNullOrWhiteSpace(sql))
            throw new Exception("SQL is empty or whitespace");
    }

    /// <summary>
    /// Parses SQL and returns the AST for inspection.
    /// Note: ScriptDom not available - returns null.
    /// </summary>
    public static object? Parse(string sql) => null;

    /// <summary>
    /// Gets statement count in a batch.
    /// Note: ScriptDom not available - returns 0.
    /// </summary>
    public static int GetStatementCount(string sql) => 0;
#endif
}
