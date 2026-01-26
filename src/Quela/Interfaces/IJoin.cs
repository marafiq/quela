namespace Quela;

/// <summary>
/// Interface after JOIN - MUST call ON before continuing.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public interface IJoin<T>
{
    /// <summary>
    /// Specifies the join condition.
    /// </summary>
    IFrom<T> On(Condition condition);
}
