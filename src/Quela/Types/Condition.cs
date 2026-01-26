namespace Quela;

/// <summary>
/// Represents a WHERE or HAVING clause condition with parameterized values.
/// </summary>
public class Condition
{
    internal string Template { get; }
    internal List<object?> Values { get; }
    internal Dictionary<string, object?>? InheritedParams { get; }

    public Condition(string template, params object?[] values)
    {
        Template = template;
        Values = values.ToList();
    }

    /// <summary>
    /// Creates a condition with inherited parameters from a subquery.
    /// </summary>
    internal Condition(string template, Dictionary<string, object?> inheritedParams)
    {
        Template = template;
        Values = new List<object?>();
        InheritedParams = inheritedParams;
    }

    /// <summary>
    /// Creates a condition with both values and inherited parameters.
    /// </summary>
    private Condition(string template, List<object?> values, Dictionary<string, object?>? inheritedParams)
    {
        Template = template;
        Values = values;
        InheritedParams = inheritedParams;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Logical Operators
    // ═══════════════════════════════════════════════════════════════════════════

    public Condition And(Condition other)
    {
        var combinedInherited = CombineInheritedParams(InheritedParams, other.InheritedParams);
        return new Condition(
            $"({Template}) AND ({other.Template})",
            Values.Concat(other.Values).ToList(),
            combinedInherited);
    }

    public Condition Or(Condition other)
    {
        var combinedInherited = CombineInheritedParams(InheritedParams, other.InheritedParams);
        return new Condition(
            $"({Template}) OR ({other.Template})",
            Values.Concat(other.Values).ToList(),
            combinedInherited);
    }

    public Condition Not() => new($"NOT ({Template})", Values.ToList(), InheritedParams);

    private static Dictionary<string, object?>? CombineInheritedParams(
        Dictionary<string, object?>? left,
        Dictionary<string, object?>? right)
    {
        if (left == null && right == null) return null;
        if (left == null) return right;
        if (right == null) return left;

        var combined = new Dictionary<string, object?>(left);
        foreach (var kvp in right)
            combined[kvp.Key] = kvp.Value;
        return combined;
    }

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

        // First, add inherited parameters (already have their names in the SQL)
        if (InheritedParams != null)
        {
            // Remap inherited params to new names using two-pass approach
            var paramList = InheritedParams.Keys
                .Where(k => k.StartsWith("@p"))
                .Select(k => (Key: k, Num: int.TryParse(k.Substring(2), out var n) ? n : -1))
                .OrderByDescending(x => x.Num)
                .ToList();

            // First pass: replace with temporary placeholders
            var tempPrefix = $"__temp_{Guid.NewGuid():N}_";
            foreach (var (oldKey, _) in paramList)
            {
                sql = sql.Replace(oldKey, tempPrefix + oldKey);
            }

            // Second pass: replace temp placeholders with new names (lowest numbers first for correct ordering)
            foreach (var (oldKey, _) in paramList.OrderBy(x => x.Num))
            {
                var newKey = $"@p{paramIndex++}";
                parameters[newKey] = InheritedParams[oldKey];
                sql = sql.Replace(tempPrefix + oldKey, newKey);
            }
        }

        // Then, add new values with placeholder replacement
        foreach (var value in Values)
        {
            var paramName = $"@p{paramIndex++}";
            parameters[paramName] = value;
            // Find the first bare @p placeholder (not followed by a digit)
            var idx = FindBarePlaceholder(sql);
            if (idx >= 0)
            {
                sql = sql[..idx] + paramName + sql[(idx + 2)..];
            }
        }

        return (sql, paramIndex);
    }

    /// <summary>
    /// Finds the first occurrence of bare @p placeholder (not followed by a digit).
    /// </summary>
    private static int FindBarePlaceholder(string sql)
    {
        int pos = 0;
        while (pos < sql.Length)
        {
            var idx = sql.IndexOf("@p", pos, StringComparison.Ordinal);
            if (idx < 0) return -1;

            var afterIdx = idx + 2;
            if (afterIdx >= sql.Length || !char.IsDigit(sql[afterIdx]))
            {
                return idx;
            }
            pos = idx + 1;
        }
        return -1;
    }
}
