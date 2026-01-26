using System.Runtime.CompilerServices;

namespace Quela;

/// <summary>
/// Base class for typed aliases used with CROSS APPLY / OUTER APPLY.
/// Property names become SQL column names via CallerMemberName.
/// </summary>
/// <example>
/// <code>
/// public class OrderTotals : TypedAlias
/// {
///     public OrderTotals(string alias = "t") : base(alias) { }
///
///     public Column&lt;decimal&gt; Total => Col&lt;decimal&gt;();     // [t].[Total]
///     public Column&lt;int&gt; ItemCount => Col&lt;int&gt;();         // [t].[ItemCount]
/// }
///
/// var t = new OrderTotals();
///
/// Sql.From(Orders)
///    .CrossApply(
///        Sql.From(OrderItems)
///           .Where(OrderItems.OrderId == Orders.Id)
///           .Select(Fn.Sum(OrderItems.Amount).As("Total"), Fn.Count().As("ItemCount")),
///        t)
///    .Where(t.Total > 1000)       // Fully typed!
///    .Select(Orders.Id, t.Total);
/// </code>
/// </example>
public abstract class TypedAlias
{
    /// <summary>
    /// The alias name used in SQL.
    /// </summary>
    public string AliasName { get; }

    protected TypedAlias(string alias)
    {
        AliasName = alias;
    }

    /// <summary>
    /// Creates a typed column reference. The property name becomes the column name.
    /// </summary>
    /// <typeparam name="T">The column type.</typeparam>
    /// <param name="column">Auto-populated with the calling property name.</param>
    protected Column<T> Col<T>([CallerMemberName] string column = "")
        => new(AliasName, column);
}
