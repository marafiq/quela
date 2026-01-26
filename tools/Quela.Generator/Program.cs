using System.Text;
using System.Text.RegularExpressions;

namespace Quela.Generator;

/// <summary>
/// CLI tool to generate type-safe C# schema classes from SQL Server database.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        var options = ParseArguments(args);

        if (options.ShowHelp || string.IsNullOrEmpty(options.ConnectionString))
        {
            ShowHelp();
            return options.ShowHelp ? 0 : 1;
        }

        try
        {
            Console.WriteLine("Quela Schema Generator");
            Console.WriteLine("══════════════════════════════════════════════════════════════");
            Console.WriteLine();

            var generator = new SchemaGenerator(options);
            var code = await generator.GenerateAsync();

            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                var outputPath = Path.GetFullPath(options.OutputFile);
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(outputPath, code);
                Console.WriteLine($"Generated: {outputPath}");
            }
            else
            {
                Console.WriteLine(code);
            }

            Console.WriteLine();
            Console.WriteLine("Schema generation completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (options.Verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    static GeneratorOptions ParseArguments(string[] args)
    {
        var options = new GeneratorOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-c":
                case "--connection":
                    if (i + 1 < args.Length)
                        options.ConnectionString = args[++i];
                    break;
                case "-o":
                case "--output":
                    if (i + 1 < args.Length)
                        options.OutputFile = args[++i];
                    break;
                case "-n":
                case "--namespace":
                    if (i + 1 < args.Length)
                        options.Namespace = args[++i];
                    break;
                case "-s":
                case "--schema":
                    if (i + 1 < args.Length)
                        options.Schema = args[++i];
                    break;
                case "--class-name":
                    if (i + 1 < args.Length)
                        options.ClassName = args[++i];
                    break;
                case "--include-views":
                    options.IncludeViews = true;
                    break;
                case "--nullable":
                    options.UseNullableReferenceTypes = true;
                    break;
                case "-v":
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
            }
        }

        // Also check environment variable for connection string
        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            options.ConnectionString = Environment.GetEnvironmentVariable("QUELA_CONNECTION_STRING") ?? "";
        }

        return options;
    }

    static void ShowHelp()
    {
        Console.WriteLine(@"Quela Schema Generator - Generate type-safe C# schema classes from SQL Server

USAGE:
    quela-gen -c <connection-string> [options]

OPTIONS:
    -c, --connection <string>   SQL Server connection string (required)
                                Can also use QUELA_CONNECTION_STRING env variable
    -o, --output <file>         Output file path (default: prints to stdout)
    -n, --namespace <name>      C# namespace (default: Database)
    -s, --schema <name>         Database schema filter (default: all schemas)
    --class-name <name>         Root class name (default: Db)
    --include-views             Include views in generation
    --nullable                  Use nullable reference types
    -v, --verbose               Verbose output
    -h, --help                  Show this help

EXAMPLES:
    # Generate schema to file
    quela-gen -c ""Server=localhost;Database=MyDb;Trusted_Connection=true"" -o Db.cs

    # Generate with custom namespace
    quela-gen -c ""Server=localhost;Database=MyDb;User Id=sa;Password=pass"" -n MyApp.Data -o Schema.cs

    # Generate only dbo schema
    quela-gen -c ""..."" -s dbo -o Db.cs

    # Use environment variable for connection
    export QUELA_CONNECTION_STRING=""Server=localhost;Database=MyDb;...""
    quela-gen -o Db.cs
");
    }
}

class GeneratorOptions
{
    public string ConnectionString { get; set; } = "";
    public string OutputFile { get; set; } = "";
    public string Namespace { get; set; } = "Database";
    public string Schema { get; set; } = "";
    public string ClassName { get; set; } = "Db";
    public bool IncludeViews { get; set; } = false;
    public bool UseNullableReferenceTypes { get; set; } = false;
    public bool Verbose { get; set; } = false;
    public bool ShowHelp { get; set; } = false;
}

class SchemaGenerator
{
    private readonly GeneratorOptions _options;

    public SchemaGenerator(GeneratorOptions options)
    {
        _options = options;
    }

    public async Task<string> GenerateAsync()
    {
        var tables = await GetTablesAsync();
        return GenerateCode(tables);
    }

    private async Task<List<TableInfo>> GetTablesAsync()
    {
        var schemaFilter = string.IsNullOrEmpty(_options.Schema) ? null : _options.Schema;
        var reader = new SchemaReader(_options.ConnectionString, schemaFilter, _options.IncludeViews);

        try
        {
            if (!await reader.TestConnectionAsync())
            {
                Console.WriteLine("Note: Unable to connect to SQL Server.");
                Console.WriteLine("Generating sample schema structure...");
                return new List<TableInfo>();
            }

            var dbName = await reader.GetDatabaseNameAsync();
            Console.WriteLine($"Connected to database: {dbName}");

            var tables = await reader.GetTablesAsync();
            Console.WriteLine($"Found {tables.Count} tables" + (_options.IncludeViews ? " and views" : ""));

            foreach (var table in tables)
            {
                if (_options.Verbose)
                    Console.WriteLine($"  - {table.Schema}.{table.Name} ({table.Columns.Count} columns)");
            }

            return tables;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: Unable to connect to SQL Server: {ex.Message}");
            Console.WriteLine("Generating sample schema structure...");
            return new List<TableInfo>();
        }
    }

    private string GenerateCode(List<TableInfo> tables)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// This code was generated by Quela.Generator");
        sb.AppendLine($"// Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("using Quela;");
        sb.AppendLine();

        if (_options.UseNullableReferenceTypes)
        {
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
        }

        sb.AppendLine($"namespace {_options.Namespace};");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Type-safe database schema for Quela SQL DSL.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {_options.ClassName}");
        sb.AppendLine("{");

        if (tables.Count == 0)
        {
            // Generate sample schema
            sb.AppendLine("    // Sample schema - connect to a real database to generate actual schema");
            sb.AppendLine();
            GenerateSampleTable(sb, "Products", new[]
            {
                ("Id", "int", false),
                ("Name", "string", false),
                ("Price", "decimal", false),
                ("CategoryId", "int", true),
                ("CreatedAt", "DateTime", false),
                ("Status", "string", true)
            });
            sb.AppendLine();
            GenerateSampleTable(sb, "Categories", new[]
            {
                ("Id", "int", false),
                ("Name", "string", false),
                ("Description", "string", true)
            });
            sb.AppendLine();
            GenerateSampleTable(sb, "Orders", new[]
            {
                ("Id", "int", false),
                ("CustomerId", "int", false),
                ("OrderDate", "DateTime", false),
                ("Total", "decimal", false),
                ("Status", "string", false)
            });
            sb.AppendLine();
            GenerateSampleTable(sb, "OrderItems", new[]
            {
                ("Id", "int", false),
                ("OrderId", "int", false),
                ("ProductId", "int", false),
                ("Quantity", "int", false),
                ("Amount", "decimal", false)
            });
            sb.AppendLine();
            GenerateSampleTable(sb, "Customers", new[]
            {
                ("Id", "int", false),
                ("Name", "string", false),
                ("Email", "string", false),
                ("CreatedAt", "DateTime", false),
                ("Status", "string", true)
            });
        }
        else
        {
            // Generate from actual database schema
            foreach (var table in tables)
            {
                GenerateTableClass(sb, table);
                sb.AppendLine();
            }
        }

        // Generate table references
        sb.AppendLine("    // ═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("    // Table References");
        sb.AppendLine("    // ═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();

        if (tables.Count == 0)
        {
            sb.AppendLine("    public static readonly Table ProductsTable = new(\"Products\", \"dbo\");");
            sb.AppendLine("    public static readonly Table CategoriesTable = new(\"Categories\", \"dbo\");");
            sb.AppendLine("    public static readonly Table OrdersTable = new(\"Orders\", \"dbo\");");
            sb.AppendLine("    public static readonly Table OrderItemsTable = new(\"OrderItems\", \"dbo\");");
            sb.AppendLine("    public static readonly Table CustomersTable = new(\"Customers\", \"dbo\");");
        }
        else
        {
            foreach (var table in tables)
            {
                var schema = string.IsNullOrEmpty(table.Schema) ? "null" : $"\"{table.Schema}\"";
                sb.AppendLine($"    public static readonly Table {table.Name}Table = new(\"{table.Name}\", {schema});");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateSampleTable(StringBuilder sb, string tableName, (string Name, string Type, bool Nullable)[] columns)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Columns for {tableName} table.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static class {tableName}");
        sb.AppendLine("    {");

        foreach (var (name, type, nullable) in columns)
        {
            var fullType = nullable ? $"{type}?" : type;
            sb.AppendLine($"        public static readonly Column<{fullType}> {name} = new(\"{tableName}\", \"{name}\");");
        }

        sb.AppendLine("    }");
    }

    private void GenerateTableClass(StringBuilder sb, TableInfo table)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Columns for {table.Schema}.{table.Name} table.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static class {SanitizeIdentifier(table.Name)}");
        sb.AppendLine("    {");

        foreach (var column in table.Columns)
        {
            var csharpType = GetCSharpType(column.SqlType, column.IsNullable);
            sb.AppendLine($"        public static readonly Column<{csharpType}> {SanitizeIdentifier(column.Name)} = new(\"{table.Name}\", \"{column.Name}\");");
        }

        sb.AppendLine("    }");
    }

    private string SanitizeIdentifier(string name)
    {
        // Remove invalid characters and ensure it's a valid C# identifier
        var sanitized = Regex.Replace(name, @"[^\w]", "_");
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;
        return sanitized;
    }

    private string GetCSharpType(string sqlType, bool isNullable)
    {
        var baseType = sqlType.ToLowerInvariant() switch
        {
            "int" or "integer" => "int",
            "bigint" => "long",
            "smallint" => "short",
            "tinyint" => "byte",
            "bit" => "bool",
            "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
            "float" => "double",
            "real" => "float",
            "datetime" or "datetime2" or "smalldatetime" => "DateTime",
            "date" => "DateOnly",
            "time" => "TimeOnly",
            "datetimeoffset" => "DateTimeOffset",
            "uniqueidentifier" => "Guid",
            "varbinary" or "binary" or "image" => "byte[]",
            _ => "string" // char, varchar, nchar, nvarchar, text, ntext, xml
        };

        if (isNullable && baseType != "string" && baseType != "byte[]")
            return baseType + "?";

        return baseType;
    }
}

public class TableInfo
{
    public string Schema { get; set; } = "";
    public string Name { get; set; } = "";
    public List<ColumnInfo> Columns { get; set; } = new();
}

public class ColumnInfo
{
    public string Name { get; set; } = "";
    public string SqlType { get; set; } = "";
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
}
