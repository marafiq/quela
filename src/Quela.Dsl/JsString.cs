namespace Quela.Dsl;

/// <summary>
/// Extension methods for string phantom operations that map to JS string methods.
/// </summary>
public static class JsStringExtensions
{
    [JsEmit("localeCompare")]
    public static int LocaleCompare(this string self, string other) => throw new PhantomTypeException();
}
