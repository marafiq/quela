namespace Quela;

/// <summary>
/// Represents a WHERE or HAVING clause condition with parameterized values.
/// </summary>
public class Condition
{
    internal string Template { get; }
    internal List<object?> Values { get; }

    public Condition(string template, params object?[] values)
    {
        Template = template;
        Values = values.ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Logical Operators
    // ═══════════════════════════════════════════════════════════════════════════

    public Condition And(Condition other) =>
        new($"({Template}) AND ({other.Template})",
            Values.Concat(other.Values).ToArray());

    public Condition Or(Condition other) =>
        new($"({Template}) OR ({other.Template})",
            Values.Concat(other.Values).ToArray());

    public Condition Not() => new($"NOT ({Template})", Values.ToArray());

    // ═══════════════════════════════════════════════════════════════════════════
    // Operator Overloads
    // ═══════════════════════════════════════════════════════════════════════════

    public static Condition operator &(Condition left, Condition right) =>
        left.And(right);

    public static Condition operator |(Condition left, Condition right) =>
        left.Or(right);

    public static Condition operator !(Condition condition) =>
        condition.Not();

    // ═══════════════════════════════════════════════════════════════════════════
    // SQL Generation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts the condition to SQL with the given parameter start index.
    /// Returns the SQL string and the next parameter index.
    /// </summary>
    internal (string Sql, int NextParamIndex) ToSql(int startParamIndex, Dictionary<string, object?> parameters)
    {
        var sql = Template;
        var paramIndex = startParamIndex;

        foreach (var value in Values)
        {
            var paramName = $"@p{paramIndex++}";
            parameters[paramName] = value;
            // Replace the first occurrence of @p with the actual parameter name
            var idx = sql.IndexOf("@p", StringComparison.Ordinal);
            if (idx >= 0)
            {
                // Check if it's just @p and not @p0, @p1, etc.
                var afterIdx = idx + 2;
                if (afterIdx >= sql.Length || !char.IsDigit(sql[afterIdx]))
                {
                    sql = sql[..idx] + paramName + sql[afterIdx..];
                }
            }
        }

        return (sql, paramIndex);
    }
}
