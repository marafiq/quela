namespace Quela;

/// <summary>
/// Represents a literal value in a SELECT clause.
/// </summary>
/// <typeparam name="T">The type of the literal value.</typeparam>
public class Literal<T> : ISelectable
{
    private readonly T _value;

    public Literal(T value)
    {
        _value = value;
    }

    /// <summary>
    /// Aliases the literal for SELECT output.
    /// </summary>
    public AliasedLiteral<T> As(string alias) => new(_value, alias);

    public string ToSql()
    {
        return _value switch
        {
            null => "NULL",
            string s => $"N'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-ddTHH:mm:ss.fff}'",
            DateOnly d => $"'{d:yyyy-MM-dd}'",
            TimeOnly t => $"'{t:HH:mm:ss.fff}'",
            decimal or double or float => _value.ToString()!,
            _ => _value.ToString()!
        };
    }
}

/// <summary>
/// A literal value with an alias.
/// </summary>
public class AliasedLiteral<T> : ISelectable
{
    private readonly T _value;
    private readonly string _alias;

    public AliasedLiteral(T value, string alias)
    {
        _value = value;
        _alias = alias;
    }

    public string ToSql()
    {
        var literal = new Literal<T>(_value).ToSql();
        return $"{literal} AS [{_alias}]";
    }
}

/// <summary>
/// Represents raw SQL for escape hatches when DSL doesn't support something.
/// </summary>
/// <typeparam name="T">The expected result type.</typeparam>
public class RawSql<T> : ISelectable, IGroupable
{
    private readonly FormattableString _sql;

    public RawSql(FormattableString sql)
    {
        _sql = sql;
    }

    /// <summary>
    /// Aliases the raw SQL for SELECT output.
    /// </summary>
    public AliasedRawSql<T> As(string alias) => new(_sql, alias);

    public string ToSql()
    {
        // Replace format arguments with their SQL representations
        var args = _sql.GetArguments();
        var format = _sql.Format;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var replacement = arg switch
            {
                IColumn col => col.FullName,
                ISelectable sel => sel.ToSql(),
                _ => arg?.ToString() ?? "NULL"
            };
            format = format.Replace($"{{{i}}}", replacement);
        }

        return format;
    }
}

/// <summary>
/// Raw SQL with an alias.
/// </summary>
public class AliasedRawSql<T> : ISelectable
{
    private readonly FormattableString _sql;
    private readonly string _alias;

    public AliasedRawSql(FormattableString sql, string alias)
    {
        _sql = sql;
        _alias = alias;
    }

    public string ToSql()
    {
        var raw = new RawSql<T>(_sql).ToSql();
        return $"({raw}) AS [{_alias}]";
    }
}
