namespace Quela;

/// <summary>
/// Represents a column with ordering direction for ORDER BY clause.
/// </summary>
public class OrderedColumn : IOrderable
{
    private readonly string _expression;
    private readonly bool _ascending;
    private NullsPosition? _nullsPosition;

    public OrderedColumn(string expression, bool ascending)
    {
        _expression = expression;
        _ascending = ascending;
    }

    /// <summary>
    /// Place NULL values first in the ordering.
    /// </summary>
    public OrderedColumn NullsFirst()
    {
        _nullsPosition = NullsPosition.First;
        return this;
    }

    /// <summary>
    /// Place NULL values last in the ordering.
    /// </summary>
    public OrderedColumn NullsLast()
    {
        _nullsPosition = NullsPosition.Last;
        return this;
    }

    public string ToSql()
    {
        var direction = _ascending ? "ASC" : "DESC";

        if (_nullsPosition == null)
            return $"{_expression} {direction}";

        // SQL Server doesn't have NULLS FIRST/LAST, so we emulate with CASE
        var nullOrder = _nullsPosition == NullsPosition.First ? 0 : 1;
        var nonNullOrder = _nullsPosition == NullsPosition.First ? 1 : 0;

        return $"(CASE WHEN {_expression} IS NULL THEN {nullOrder} ELSE {nonNullOrder} END), {_expression} {direction}";
    }

    private enum NullsPosition
    {
        First,
        Last
    }
}
