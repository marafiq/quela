using System.Linq.Expressions;

namespace Quela.Dsl;

/// <summary>
/// Builds multi-statement JavaScript blocks from a sequence of C# expression trees.
/// Handles var declarations, assignments, if/else, and expression statements.
/// </summary>
public sealed class JsBlockBuilder
{
    private readonly JsExpressionVisitor _visitor;
    private readonly List<string> _lines = new();

    public JsBlockBuilder(JsNamingConvention? naming = null)
    {
        _visitor = new JsExpressionVisitor(naming);
    }

    /// <summary>
    /// Adds a variable declaration: var name = expression;
    /// </summary>
    public JsBlockBuilder Var(string name, Expression expression)
    {
        var js = _visitor.Transpile(expression);
        _lines.Add($"var {name} = {js};");
        return this;
    }

    /// <summary>
    /// Adds an assignment statement: target = value;
    /// </summary>
    public JsBlockBuilder Assign(string target, Expression expression)
    {
        var js = _visitor.Transpile(expression);
        _lines.Add($"{target} = {js};");
        return this;
    }

    /// <summary>
    /// Adds an expression statement (e.g., method call): expression;
    /// </summary>
    public JsBlockBuilder Statement(Expression expression)
    {
        var js = _visitor.Transpile(expression);
        _lines.Add($"{js};");
        return this;
    }

    /// <summary>
    /// Adds an if/else block.
    /// </summary>
    public JsBlockBuilder IfElse(Expression condition, Action<JsBlockBuilder> thenBlock, Action<JsBlockBuilder>? elseBlock = null)
    {
        var condJs = _visitor.Transpile(condition);
        var thenBuilder = new JsBlockBuilder();
        thenBlock(thenBuilder);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"if ({condJs}) {{");
        foreach (var line in thenBuilder._lines)
            sb.AppendLine($"    {line}");
        sb.Append('}');

        if (elseBlock is not null)
        {
            var elseBuilder = new JsBlockBuilder();
            elseBlock(elseBuilder);
            sb.AppendLine(" else {");
            foreach (var line in elseBuilder._lines)
                sb.AppendLine($"    {line}");
            sb.Append('}');
        }

        _lines.Add(sb.ToString());
        return this;
    }

    /// <summary>
    /// Adds a raw JavaScript line (for edge cases).
    /// </summary>
    public JsBlockBuilder Raw(string js)
    {
        _lines.Add(js);
        return this;
    }

    /// <summary>
    /// Builds the final JavaScript string.
    /// </summary>
    public string Build()
    {
        return string.Join("\n", _lines);
    }

    public override string ToString() => Build();
}
