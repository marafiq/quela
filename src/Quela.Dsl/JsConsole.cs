namespace Quela.Dsl;

/// <summary>
/// Phantom type for JavaScript console object.
/// </summary>
[JsType("console")]
public static class JsConsole
{
    [JsEmit("log")]
    public static void Log(object value) => throw new PhantomTypeException();

    [JsEmit("warn")]
    public static void Warn(object value) => throw new PhantomTypeException();

    [JsEmit("error")]
    public static void Error(object value) => throw new PhantomTypeException();
}
