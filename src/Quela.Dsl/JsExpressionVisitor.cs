using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Quela.Dsl;

/// <summary>
/// Walks a C# expression tree and emits equivalent JavaScript.
/// Reads [JsEmit] attributes from phantom types to determine JS method/property names.
/// </summary>
public sealed class JsExpressionVisitor
{
    private readonly StringBuilder _sb = new();
    private readonly JsNamingConvention _naming;

    public JsExpressionVisitor(JsNamingConvention? naming = null)
    {
        _naming = naming ?? JsNamingConvention.Default;
    }

    /// <summary>
    /// Transpiles a C# expression tree to a JavaScript string.
    /// </summary>
    public string Transpile(Expression expression)
    {
        _sb.Clear();
        Visit(expression);
        return _sb.ToString();
    }

    /// <summary>
    /// Transpiles a lambda expression, extracting just the body.
    /// Useful for top-level statement transpilation.
    /// </summary>
    public string TranspileBody(LambdaExpression lambda)
    {
        _sb.Clear();
        Visit(lambda.Body);
        return _sb.ToString();
    }

    private void Visit(Expression? node)
    {
        if (node is null) return;

        switch (node)
        {
            case BinaryExpression binary:
                VisitBinary(binary);
                break;
            case UnaryExpression unary:
                VisitUnary(unary);
                break;
            case MethodCallExpression call:
                VisitMethodCall(call);
                break;
            case MemberExpression member:
                VisitMember(member);
                break;
            case ConstantExpression constant:
                VisitConstant(constant);
                break;
            case ParameterExpression param:
                VisitParameter(param);
                break;
            case LambdaExpression lambda:
                VisitLambda(lambda);
                break;
            case ConditionalExpression conditional:
                VisitConditional(conditional);
                break;
            case IndexExpression index:
                VisitIndex(index);
                break;
            case NewArrayExpression newArray:
                VisitNewArray(newArray);
                break;
            case InvocationExpression invocation:
                VisitInvocation(invocation);
                break;
            case MemberInitExpression memberInit:
                VisitMemberInit(memberInit);
                break;
            default:
                _sb.Append($"/* unsupported: {node.NodeType} */");
                break;
        }
    }

    // ── Binary expressions: a + b, a == b, etc. ─────────────────────────
    private void VisitBinary(BinaryExpression node)
    {
        var jsOp = node.NodeType switch
        {
            ExpressionType.Equal => "===",
            ExpressionType.NotEqual => "!==",
            ExpressionType.Add => IsStringConcat(node) ? "+" : "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Modulo => "%",
            ExpressionType.AndAlso => "&&",
            ExpressionType.OrElse => "||",
            ExpressionType.GreaterThan => ">",
            ExpressionType.LessThan => "<",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.ArrayIndex => null, // handled specially
            _ => $"/* {node.NodeType} */"
        };

        if (node.NodeType == ExpressionType.ArrayIndex)
        {
            Visit(node.Left);
            _sb.Append('[');
            Visit(node.Right);
            _sb.Append(']');
            return;
        }

        Visit(node.Left);
        _sb.Append($" {jsOp} ");
        Visit(node.Right);
    }

    private static bool IsStringConcat(BinaryExpression node)
        => node.Type == typeof(string) || node.Method?.Name == "Concat";

    // ── Unary expressions: !a, (cast)a ──────────────────────────────────
    private void VisitUnary(UnaryExpression node)
    {
        switch (node.NodeType)
        {
            case ExpressionType.Not:
                _sb.Append('!');
                Visit(node.Operand);
                break;
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
                // In JS, ignore C# casts — just emit the operand
                Visit(node.Operand);
                break;
            case ExpressionType.Quote:
                // Quote wraps a lambda inside Expression<> — unwrap it
                Visit(node.Operand);
                break;
            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                _sb.Append('-');
                Visit(node.Operand);
                break;
            default:
                Visit(node.Operand);
                break;
        }
    }

    // ── Method calls: the core of JsArray transpilation ─────────────────
    private void VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;
        var jsEmit = method.GetCustomAttribute<JsEmitAttribute>();

        // Check if it's an extension method with [JsEmit]
        if (jsEmit is null && method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
        {
            // Extension methods — check for JsEmit on the method itself
            jsEmit = method.GetCustomAttribute<JsEmitAttribute>();
        }

        if (jsEmit is not null)
        {
            // Special case: {index} pattern → bracket access (indexer)
            if (jsEmit.Name == "{index}" && node.Object is not null)
            {
                Visit(node.Object);
                _sb.Append('[');
                for (int i = 0; i < node.Arguments.Count; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    Visit(node.Arguments[i]);
                }
                _sb.Append(']');
                return;
            }

            // Instance method on phantom type or extension method
            if (node.Object is not null)
            {
                // Instance: obj.method(args)
                Visit(node.Object);
                _sb.Append('.');
                _sb.Append(jsEmit.Name);
                _sb.Append('(');
                EmitArguments(node.Arguments, method);
                _sb.Append(')');
            }
            else if (method.IsStatic && node.Arguments.Count > 0
                     && method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
            {
                // Extension method: first arg is 'this'
                Visit(node.Arguments[0]);
                _sb.Append('.');
                _sb.Append(jsEmit.Name);
                _sb.Append('(');
                EmitArguments(node.Arguments.Skip(1).ToList(), method);
                _sb.Append(')');
            }
            else
            {
                // Static method on phantom type (e.g., JsConsole.Log)
                var declaringType = method.DeclaringType;
                var jsType = declaringType?.GetCustomAttribute<JsTypeAttribute>();
                var objName = jsType?.Name ?? _naming.ConvertTypeName(declaringType?.Name ?? "unknown");
                _sb.Append(objName);
                _sb.Append('.');
                _sb.Append(jsEmit.Name);
                _sb.Append('(');
                EmitArguments(node.Arguments, method);
                _sb.Append(')');
            }
            return;
        }

        // Check if the declaring type has [JsEmit] on this method
        var declaringTypeForLookup = method.DeclaringType;
        if (declaringTypeForLookup is not null)
        {
            // Look for [JsEmit] on the method in the declaring type or its generic definition
            var methodToCheck = method;
            if (declaringTypeForLookup.IsGenericType)
            {
                var genericDef = declaringTypeForLookup.GetGenericTypeDefinition();
                var genericMethod = genericDef.GetMethods()
                    .FirstOrDefault(m => m.Name == method.Name && m.GetParameters().Length == method.GetParameters().Length);
                if (genericMethod is not null)
                {
                    jsEmit = genericMethod.GetCustomAttribute<JsEmitAttribute>();
                    if (jsEmit is not null)
                    {
                        if (node.Object is not null)
                        {
                            Visit(node.Object);
                            _sb.Append('.');
                            _sb.Append(jsEmit.Name);
                            _sb.Append('(');
                            EmitArguments(node.Arguments, genericMethod);
                            _sb.Append(')');
                        }
                        return;
                    }
                }
            }
        }

        // String.Concat — emit as +
        if (method.Name == "Concat" && method.DeclaringType == typeof(string))
        {
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                if (i > 0) _sb.Append(" + ");
                Visit(node.Arguments[i]);
            }
            return;
        }

        // Default: emit as obj.methodName(args) with camelCase
        if (node.Object is not null)
        {
            Visit(node.Object);
            _sb.Append('.');
            _sb.Append(_naming.ConvertMethodName(method.Name));
            _sb.Append('(');
            EmitArguments(node.Arguments, method);
            _sb.Append(')');
        }
        else
        {
            _sb.Append(_naming.ConvertMethodName(method.Name));
            _sb.Append('(');
            EmitArguments(node.Arguments, method);
            _sb.Append(')');
        }
    }

    private void EmitArguments(IList<Expression> args, MethodInfo method)
    {
        var parameters = method.GetParameters();
        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0) _sb.Append(", ");

            var arg = args[i];

            // Unwrap Quote nodes to get at the lambda
            if (arg is UnaryExpression { NodeType: ExpressionType.Quote } quote)
                arg = quote.Operand;

            if (arg is LambdaExpression lambda)
            {
                // Determine if this is a void (Action) lambda
                bool isVoid = lambda.ReturnType == typeof(void);
                EmitLambda(lambda, isVoid);
            }
            else
            {
                Visit(arg);
            }
        }
    }

    // ── Lambda → function(params) { return body; } ──────────────────────
    private void EmitLambda(LambdaExpression lambda, bool isVoid)
    {
        _sb.Append("function(");
        for (int i = 0; i < lambda.Parameters.Count; i++)
        {
            if (i > 0) _sb.Append(", ");
            _sb.Append(lambda.Parameters[i].Name);
        }
        _sb.Append(") { ");
        if (!isVoid) _sb.Append("return ");
        Visit(lambda.Body);
        _sb.Append("; }");
    }

    // ── Member access: obj.Property ─────────────────────────────────────
    private void VisitMember(MemberExpression node)
    {
        // Check for [JsEmit] attribute
        var jsEmit = node.Member.GetCustomAttribute<JsEmitAttribute>();

        // For generic types, check the generic type definition
        if (jsEmit is null && node.Member.DeclaringType is { IsGenericType: true } declType)
        {
            var genericDef = declType.GetGenericTypeDefinition();
            var genericMember = genericDef.GetMember(node.Member.Name).FirstOrDefault();
            jsEmit = genericMember?.GetCustomAttribute<JsEmitAttribute>();
        }

        if (jsEmit is not null)
        {
            if (node.Expression is not null)
            {
                Visit(node.Expression);
                _sb.Append('.');
            }
            _sb.Append(jsEmit.Name);
            return;
        }

        // Check if it's a closure variable (captured from outer scope)
        if (node.Expression is ConstantExpression ce && node.Member is FieldInfo fi)
        {
            // This is a captured variable — emit its name as a JS identifier
            _sb.Append(_naming.ConvertIdentifier(fi.Name));
            return;
        }

        // Default: emit as obj.camelCaseProp
        if (node.Expression is not null)
        {
            Visit(node.Expression);
            _sb.Append('.');
        }
        _sb.Append(_naming.ConvertPropertyName(node.Member.Name));
    }

    // ── Constants ───────────────────────────────────────────────────────
    private void VisitConstant(ConstantExpression node)
    {
        switch (node.Value)
        {
            case null:
                _sb.Append("null");
                break;
            case string s:
                _sb.Append('\'');
                _sb.Append(s.Replace("'", "\\'"));
                _sb.Append('\'');
                break;
            case bool b:
                _sb.Append(b ? "true" : "false");
                break;
            case int i:
                _sb.Append(i);
                break;
            case long l:
                _sb.Append(l);
                break;
            case double d:
                _sb.Append(d);
                break;
            case float f:
                _sb.Append(f);
                break;
            case decimal m:
                _sb.Append(m);
                break;
            default:
                // Could be a closure object — skip emission
                // (the member access on it will handle the variable name)
                break;
        }
    }

    // ── Parameters (lambda params: r, a, b, sum, item) ──────────────────
    private void VisitParameter(ParameterExpression node)
    {
        _sb.Append(node.Name);
    }

    // ── Lambda (standalone) ─────────────────────────────────────────────
    private void VisitLambda(LambdaExpression node)
    {
        EmitLambda(node, node.ReturnType == typeof(void));
    }

    // ── Conditional: a ? b : c ──────────────────────────────────────────
    private void VisitConditional(ConditionalExpression node)
    {
        Visit(node.Test);
        _sb.Append(" ? ");
        Visit(node.IfTrue);
        _sb.Append(" : ");
        Visit(node.IfFalse);
    }

    // ── Index: obj[idx] ─────────────────────────────────────────────────
    private void VisitIndex(IndexExpression node)
    {
        Visit(node.Object);
        _sb.Append('[');
        Visit(node.Arguments[0]);
        _sb.Append(']');
    }

    // ── NewArray: new[] { a, b, c } → [a, b, c] ────────────────────────
    private void VisitNewArray(NewArrayExpression node)
    {
        _sb.Append('[');
        for (int i = 0; i < node.Expressions.Count; i++)
        {
            if (i > 0) _sb.Append(", ");
            Visit(node.Expressions[i]);
        }
        _sb.Append(']');
    }

    // ── Invocation ──────────────────────────────────────────────────────
    private void VisitInvocation(InvocationExpression node)
    {
        Visit(node.Expression);
        _sb.Append('(');
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            if (i > 0) _sb.Append(", ");
            Visit(node.Arguments[i]);
        }
        _sb.Append(')');
    }

    // ── Member init: new Foo { Bar = 1 } → { bar: 1 } ──────────────────
    private void VisitMemberInit(MemberInitExpression node)
    {
        _sb.Append("{ ");
        for (int i = 0; i < node.Bindings.Count; i++)
        {
            if (i > 0) _sb.Append(", ");
            if (node.Bindings[i] is MemberAssignment assign)
            {
                _sb.Append(_naming.ConvertPropertyName(assign.Member.Name));
                _sb.Append(": ");
                Visit(assign.Expression);
            }
        }
        _sb.Append(" }");
    }
}
