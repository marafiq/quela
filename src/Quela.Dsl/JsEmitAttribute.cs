namespace Quela.Dsl;

/// <summary>
/// Marks a phantom-type member with the JavaScript name it should emit.
/// The expression visitor reads this attribute to produce the correct JS output.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
public sealed class JsEmitAttribute : Attribute
{
    public string Name { get; }

    public JsEmitAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Marks a phantom type with its JavaScript type name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class JsTypeAttribute : Attribute
{
    public string Name { get; }

    public JsTypeAttribute(string name)
    {
        Name = name;
    }
}
