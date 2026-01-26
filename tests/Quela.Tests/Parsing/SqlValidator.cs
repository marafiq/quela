using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Quela.Tests.Parsing;

/// <summary>
/// SQL Server syntax validator using ScriptDom.
/// Validates that generated SQL is syntactically correct without requiring a database.
/// </summary>
public static class SqlValidator
{
    private static readonly TSql160Parser Parser = new(initialQuotedIdentifiers: true);

    /// <summary>
    /// Validates SQL syntax using SQL Server's official parser.
    /// </summary>
    /// <param name="sql">The SQL string to validate.</param>
    /// <returns>A tuple with IsValid flag and list of error messages.</returns>
    public static (bool IsValid, IList<string> Errors) Validate(string sql)
    {
        using var reader = new StringReader(sql);
        Parser.Parse(reader, out var errors);

        var errorMessages = errors
            .Select(e => $"Line {e.Line}, Col {e.Column}: {e.Message}")
            .ToList();

        return (errors.Count == 0, errorMessages);
    }

    /// <summary>
    /// Asserts that the SQL is valid. Throws if invalid.
    /// </summary>
    public static void AssertValid(string sql)
    {
        var (isValid, errors) = Validate(sql);
        if (!isValid)
        {
            var errorText = string.Join("\n", errors);
            throw new InvalidOperationException($"Invalid SQL:\n{sql}\n\nErrors:\n{errorText}");
        }
    }
}
