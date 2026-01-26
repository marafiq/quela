namespace Quela;

/// <summary>
/// Extension methods for conditional query building.
/// </summary>
public static class ConditionalExtensions
{
    /// <summary>
    /// Conditionally adds a WHERE clause only if the condition is true.
    /// </summary>
    /// <example>
    /// <code>
    /// Sql.From(Residents)
    ///    .Where(Residents.FacilityId == facilityId)
    ///    .WhereIf(unitId.HasValue, Residents.UnitId == unitId)
    ///    .WhereIf(!string.IsNullOrEmpty(name), Residents.FullName.Contains(name))
    ///    .Select(Residents.Id, Residents.FullName);
    /// </code>
    /// </example>
    public static ICondition<T> WhereIf<T>(this IWhere<T> query, bool condition, Condition sqlCondition)
    {
        if (condition)
            return query.Where(sqlCondition);

        // Return as ICondition by wrapping in a no-op condition
        // This is a bit of a hack but maintains the fluent interface
        return query.Where(new Condition("1 = 1")).And(new Condition("1 = 1"));
    }

    /// <summary>
    /// Conditionally adds a WHERE clause only if the condition is true.
    /// Uses a factory function to create the condition lazily.
    /// </summary>
    public static ICondition<T> WhereIf<T>(this IWhere<T> query, bool condition, Func<Condition> conditionFactory)
    {
        if (condition)
            return query.Where(conditionFactory());

        return query.Where(new Condition("1 = 1"));
    }

    /// <summary>
    /// Conditionally adds an AND clause only if the condition is true.
    /// </summary>
    public static ICondition<T> AndIf<T>(this ICondition<T> query, bool condition, Condition sqlCondition)
    {
        return condition ? query.And(sqlCondition) : query;
    }

    /// <summary>
    /// Conditionally adds an AND clause only if the condition is true.
    /// Uses a factory function to create the condition lazily.
    /// </summary>
    public static ICondition<T> AndIf<T>(this ICondition<T> query, bool condition, Func<Condition> conditionFactory)
    {
        return condition ? query.And(conditionFactory()) : query;
    }

    /// <summary>
    /// Conditionally adds an OR clause only if the condition is true.
    /// </summary>
    public static ICondition<T> OrIf<T>(this ICondition<T> query, bool condition, Condition sqlCondition)
    {
        return condition ? query.Or(sqlCondition) : query;
    }
}

/// <summary>
/// Extension methods for string columns.
/// </summary>
public static class StringColumnExtensions
{
    /// <summary>
    /// Creates a LIKE condition with the specified pattern.
    /// </summary>
    public static Condition Like(this Column<string> col, string pattern)
        => new($"{col.FullName} LIKE @p", pattern);

    /// <summary>
    /// Creates a NOT LIKE condition with the specified pattern.
    /// </summary>
    public static Condition NotLike(this Column<string> col, string pattern)
        => new($"{col.FullName} NOT LIKE @p", pattern);

    /// <summary>
    /// Creates a LIKE condition that matches anywhere in the string.
    /// </summary>
    public static Condition Contains(this Column<string> col, string value)
        => new($"{col.FullName} LIKE @p", $"%{EscapeLikePattern(value)}%");

    /// <summary>
    /// Creates a LIKE condition that matches the start of the string.
    /// </summary>
    public static Condition StartsWith(this Column<string> col, string value)
        => new($"{col.FullName} LIKE @p", $"{EscapeLikePattern(value)}%");

    /// <summary>
    /// Creates a LIKE condition that matches the end of the string.
    /// </summary>
    public static Condition EndsWith(this Column<string> col, string value)
        => new($"{col.FullName} LIKE @p", $"%{EscapeLikePattern(value)}");

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
    }
}

/// <summary>
/// Extension methods for comparable columns.
/// </summary>
public static class ComparableColumnExtensions
{
    /// <summary>
    /// Creates a BETWEEN condition.
    /// </summary>
    public static Condition Between<T>(this Column<T> col, T low, T high)
        => new($"{col.FullName} BETWEEN @p AND @p", low!, high!);

    /// <summary>
    /// Creates a NOT BETWEEN condition.
    /// </summary>
    public static Condition NotBetween<T>(this Column<T> col, T low, T high)
        => new($"{col.FullName} NOT BETWEEN @p AND @p", low!, high!);
}

/// <summary>
/// Extension methods for column IN subquery.
/// </summary>
public static class SubqueryColumnExtensions
{
    /// <summary>
    /// Creates an IN (subquery) condition.
    /// </summary>
    public static Condition In<T>(this Column<T> col, IQuery subquery)
    {
        var result = subquery.Build();
        var inheritedParams = new Dictionary<string, object?>(result.Parameters);
        return new Condition($"{col.FullName} IN ({result.Sql})", inheritedParams);
    }

    /// <summary>
    /// Creates a NOT IN (subquery) condition.
    /// </summary>
    public static Condition NotIn<T>(this Column<T> col, IQuery subquery)
    {
        var result = subquery.Build();
        var inheritedParams = new Dictionary<string, object?>(result.Parameters);
        return new Condition($"{col.FullName} NOT IN ({result.Sql})", inheritedParams);
    }
}
