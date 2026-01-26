using System.Text.RegularExpressions;

namespace Quela.Tests;

/// <summary>
/// Basic SQL syntax validator for T-SQL.
/// Validates common SQL patterns without external dependencies.
/// </summary>
public static class SqlValidator
{
    /// <summary>
    /// Validates SQL syntax and throws if invalid.
    /// </summary>
    public static void AssertValid(string sql)
    {
        var errors = Validate(sql);
        if (errors.Count > 0)
        {
            throw new Exception($"SQL validation failed:\n{string.Join("\n", errors)}\n\nSQL:\n{sql}");
        }
    }

    /// <summary>
    /// Validates SQL syntax and returns list of errors.
    /// </summary>
    public static List<string> Validate(string sql)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(sql))
        {
            errors.Add("SQL is empty");
            return errors;
        }

        // Check for balanced parentheses
        var parenCount = 0;
        foreach (var c in sql)
        {
            if (c == '(') parenCount++;
            if (c == ')') parenCount--;
            if (parenCount < 0)
            {
                errors.Add("Unbalanced parentheses: extra closing parenthesis");
                break;
            }
        }
        if (parenCount > 0)
            errors.Add($"Unbalanced parentheses: {parenCount} unclosed");

        // Check for balanced square brackets
        var bracketCount = 0;
        foreach (var c in sql)
        {
            if (c == '[') bracketCount++;
            if (c == ']') bracketCount--;
            if (bracketCount < 0)
            {
                errors.Add("Unbalanced square brackets: extra closing bracket");
                break;
            }
        }
        if (bracketCount > 0)
            errors.Add($"Unbalanced square brackets: {bracketCount} unclosed");

        // Check for basic SQL structure
        var upperSql = sql.ToUpperInvariant();

        // Must have SELECT
        if (!upperSql.Contains("SELECT"))
            errors.Add("Missing SELECT clause");

        // If has FROM, it must come after SELECT
        var selectIndex = upperSql.IndexOf("SELECT");
        var fromIndex = upperSql.IndexOf(" FROM ");
        if (fromIndex >= 0 && fromIndex < selectIndex)
            errors.Add("FROM appears before SELECT");

        // If has WHERE, it must come after FROM
        var whereIndex = upperSql.IndexOf(" WHERE ");
        if (whereIndex >= 0 && fromIndex >= 0 && whereIndex < fromIndex)
            errors.Add("WHERE appears before FROM");

        // If has GROUP BY, it must come after WHERE (or FROM if no WHERE)
        var groupByIndex = upperSql.IndexOf(" GROUP BY ");
        if (groupByIndex >= 0)
        {
            if (whereIndex >= 0 && groupByIndex < whereIndex)
                errors.Add("GROUP BY appears before WHERE");
            else if (whereIndex < 0 && fromIndex >= 0 && groupByIndex < fromIndex)
                errors.Add("GROUP BY appears before FROM");
        }

        // If has HAVING, it must come after GROUP BY
        var havingIndex = upperSql.IndexOf(" HAVING ");
        if (havingIndex >= 0 && groupByIndex >= 0 && havingIndex < groupByIndex)
            errors.Add("HAVING appears before GROUP BY");

        // If has ORDER BY, it must be near the end (after SELECT/WHERE/GROUP BY)
        var orderByIndex = upperSql.IndexOf(" ORDER BY ");
        if (orderByIndex >= 0)
        {
            if (havingIndex >= 0 && orderByIndex < havingIndex)
                errors.Add("ORDER BY appears before HAVING");
            else if (groupByIndex >= 0 && orderByIndex < groupByIndex)
                errors.Add("ORDER BY appears before GROUP BY");
        }

        // Check for common syntax errors
        if (Regex.IsMatch(sql, @",,"))
            errors.Add("Double comma found");

        // Note: Empty parentheses () are valid for functions like ROW_NUMBER(), RANK(), etc.
        // so we don't check for that pattern.

        if (Regex.IsMatch(sql, @"SELECT\s+FROM", RegexOptions.IgnoreCase))
            errors.Add("SELECT with no columns before FROM");

        // Check for incomplete clauses
        if (Regex.IsMatch(upperSql, @"WHERE\s*$"))
            errors.Add("Incomplete WHERE clause");

        if (Regex.IsMatch(upperSql, @"AND\s*$"))
            errors.Add("Incomplete AND clause");

        if (Regex.IsMatch(upperSql, @"OR\s*$"))
            errors.Add("Incomplete OR clause");

        if (Regex.IsMatch(upperSql, @"ORDER BY\s*$"))
            errors.Add("Incomplete ORDER BY clause");

        if (Regex.IsMatch(upperSql, @"GROUP BY\s*$"))
            errors.Add("Incomplete GROUP BY clause");

        // Check JOIN syntax
        if (Regex.IsMatch(upperSql, @"(INNER|LEFT|RIGHT|FULL|CROSS)\s+JOIN.*?(INNER|LEFT|RIGHT|FULL|CROSS)\s+JOIN")
            && !Regex.IsMatch(upperSql, @"(INNER|LEFT|RIGHT|FULL)\s+(OUTER\s+)?JOIN.*?ON"))
        {
            // Multiple joins - check each has ON (except CROSS JOIN)
            var joinMatches = Regex.Matches(upperSql, @"(INNER|LEFT|RIGHT|FULL)\s+(OUTER\s+)?JOIN");
            var onMatches = Regex.Matches(upperSql, @"\bON\b");
            // This is a simplified check
        }

        // Check for dangling operators
        if (Regex.IsMatch(sql, @"[=<>!]+\s*$"))
            errors.Add("Dangling comparison operator");

        // Check CTE syntax
        if (upperSql.Contains("WITH ") && upperSql.Contains(" AS ("))
        {
            // Basic CTE validation - must have matching AS ( ... )
            var withIndex = upperSql.IndexOf("WITH ");
            var asIndex = upperSql.IndexOf(" AS (", withIndex);
            if (asIndex < 0)
                errors.Add("CTE missing AS clause");
        }

        // Check UNION/INTERSECT/EXCEPT have SELECT somewhere after the operator
        // (This is a simplified check - the actual query may have the SELECT embedded)
        foreach (var setOp in new[] { "UNION ALL", "UNION", "INTERSECT", "EXCEPT" })
        {
            var setOpIndex = upperSql.IndexOf(setOp);
            if (setOpIndex > 0)
            {
                var afterOp = upperSql.Substring(setOpIndex + setOp.Length);
                if (!afterOp.Contains("SELECT"))
                    errors.Add($"{setOp} must be followed by SELECT");
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates that the SQL contains expected keywords/patterns.
    /// </summary>
    public static void AssertContains(string sql, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (!sql.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"SQL does not contain expected pattern: '{pattern}'\n\nSQL:\n{sql}");
            }
        }
    }

    /// <summary>
    /// Validates that the SQL does NOT contain certain patterns.
    /// </summary>
    public static void AssertNotContains(string sql, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (sql.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"SQL contains unexpected pattern: '{pattern}'\n\nSQL:\n{sql}");
            }
        }
    }
}
