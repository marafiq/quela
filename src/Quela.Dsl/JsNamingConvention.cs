namespace Quela.Dsl;

/// <summary>
/// Controls how C# PascalCase names are converted to JavaScript camelCase.
/// Also handles identifier mapping (Grid → residentGrid, etc.)
/// </summary>
public sealed class JsNamingConvention
{
    private readonly Dictionary<string, string> _identifierMap;
    private readonly Dictionary<string, string> _propertyMap;

    public static JsNamingConvention Default { get; } = new(
        identifierMap: new Dictionary<string, string>(),
        propertyMap: new Dictionary<string, string>()
    );

    public JsNamingConvention(
        Dictionary<string, string> identifierMap,
        Dictionary<string, string> propertyMap)
    {
        _identifierMap = identifierMap;
        _propertyMap = propertyMap;
    }

    /// <summary>Converts a C# identifier (variable/field) to JS.</summary>
    public string ConvertIdentifier(string name)
    {
        if (_identifierMap.TryGetValue(name, out var mapped)) return mapped;
        return ToCamelCase(name);
    }

    /// <summary>Converts a C# property name to JS.</summary>
    public string ConvertPropertyName(string name)
    {
        if (_propertyMap.TryGetValue(name, out var mapped)) return mapped;
        return ToCamelCase(name);
    }

    /// <summary>Converts a C# method name to JS camelCase.</summary>
    public string ConvertMethodName(string name) => ToCamelCase(name);

    /// <summary>Converts a C# type name to JS.</summary>
    public string ConvertTypeName(string name) => ToCamelCase(name);

    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (char.IsLower(name[0])) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
