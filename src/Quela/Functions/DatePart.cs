namespace Quela;

/// <summary>
/// SQL Server date parts for date functions.
/// </summary>
public enum DatePart
{
    Year,
    Quarter,
    Month,
    DayOfYear,
    Day,
    Week,
    Weekday,
    Hour,
    Minute,
    Second,
    Millisecond,
    Microsecond,
    Nanosecond
}

public static class DatePartExtensions
{
    public static string ToSql(this DatePart datePart) => datePart switch
    {
        DatePart.Year => "YEAR",
        DatePart.Quarter => "QUARTER",
        DatePart.Month => "MONTH",
        DatePart.DayOfYear => "DAYOFYEAR",
        DatePart.Day => "DAY",
        DatePart.Week => "WEEK",
        DatePart.Weekday => "WEEKDAY",
        DatePart.Hour => "HOUR",
        DatePart.Minute => "MINUTE",
        DatePart.Second => "SECOND",
        DatePart.Millisecond => "MILLISECOND",
        DatePart.Microsecond => "MICROSECOND",
        DatePart.Nanosecond => "NANOSECOND",
        _ => throw new ArgumentOutOfRangeException(nameof(datePart))
    };
}
