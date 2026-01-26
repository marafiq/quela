using System.Runtime.CompilerServices;

namespace Quela;

/// <summary>
/// Base class for reusable query patterns with typed column access.
/// Subclasses define typed columns and build a query with automatic tenant filtering.
/// </summary>
/// <example>
/// <code>
/// public class ActiveResidents : TypedView
/// {
///     public ActiveResidents(string alias = "r") : base(alias) { }
///
///     public Column&lt;int&gt; Id => Col&lt;int&gt;();
///     public Column&lt;string&gt; FullName => Col&lt;string&gt;();
///     public Column&lt;int&gt; UnitId => Col&lt;int&gt;();
///
///     public override IQuery Build(QueryContext ctx) =>
///         Sql.From(Residents)
///            .Where(Residents.FacilityId == ctx.FacilityId)
///            .Where(Residents.Status == "Active")
///            .Select(
///                Residents.Id.As("Id"),
///                Residents.FullName.As("FullName"),
///                Residents.UnitId.As("UnitId"));
/// }
///
/// // Usage:
/// var residents = new ActiveResidents();
/// var ctx = QueryContext.ForFacility(1, 42, "user1");
///
/// Sql.From(residents.Build(ctx), residents.ViewAlias)
///    .Where(residents.UnitId == 5)
///    .Select(residents.Id, residents.FullName);
/// </code>
/// </example>
public abstract class TypedView
{
    /// <summary>
    /// The alias used for this view when used as a subquery.
    /// </summary>
    public string ViewAlias { get; }

    protected TypedView(string alias)
    {
        ViewAlias = alias;
    }

    /// <summary>
    /// Creates a typed column reference. The property name becomes the column name.
    /// </summary>
    protected Column<T> Col<T>([CallerMemberName] string column = "")
        => new(ViewAlias, column);

    /// <summary>
    /// Builds the underlying query with the given context.
    /// </summary>
    public abstract IQuery Build(QueryContext ctx);
}

/// <summary>
/// Extension methods for using TypedView.
/// </summary>
public static class TypedViewExtensions
{
    /// <summary>
    /// Creates a FROM clause using a TypedView with context.
    /// </summary>
    public static IFrom<Row> From<TView>(this TView view, QueryContext ctx) where TView : TypedView
    {
        return Sql.From(view.Build(ctx), view.ViewAlias);
    }
}
