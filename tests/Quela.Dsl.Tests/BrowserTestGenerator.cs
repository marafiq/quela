using System.Linq.Expressions;
using System.Text.Json;
using Quela.Dsl;

namespace Quela.Dsl.Tests;

/// <summary>
/// Generates a JSON manifest of transpiled JS snippets from real C# expression trees.
/// This manifest is consumed by the browser test page to execute and verify each example.
/// </summary>
public static class BrowserTestGenerator
{
    public record TestCase(string Id, string Title, string CSharp, string JavaScript, string VerifyJs);

    public static List<TestCase> GenerateAll()
    {
        var visitor = new JsExpressionVisitor();
        var cases = new List<TestCase>();

        // §5.1 Filter
        {
            Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
                residents => residents.Filter(r => r.IsActive);
            cases.Add(new("5.1", "Filter",
                "residents.Filter(r => r.IsActive)",
                visitor.Transpile(expr.Body),
                "result.length === 3 && result.every(function(r) { return r.isActive; })"));
        }

        // §5.2 Map
        {
            Expression<Func<JsArray<Resident>, JsArray<string>>> expr =
                residents => residents.Map(r => r.Name);
            cases.Add(new("5.2", "Map",
                "residents.Map(r => r.Name)",
                visitor.Transpile(expr.Body),
                "JSON.stringify(result) === JSON.stringify(['Alice','Bob','Charlie','Diana'])"));
        }

        // §5.3 Filter + Map Chain
        {
            Expression<Func<JsArray<Resident>, JsArray<string>>> expr =
                residents => residents.Filter(r => r.IsActive).Map(r => r.Name);
            cases.Add(new("5.3", "Filter + Map Chain",
                "residents.Filter(r => r.IsActive).Map(r => r.Name)",
                visitor.Transpile(expr.Body),
                "JSON.stringify(result) === JSON.stringify(['Alice','Charlie','Diana'])"));
        }

        // §5.4 Find
        {
            var selectedId = 3;
            Expression<Func<JsArray<Resident>, Resident>> expr =
                residents => residents.Find(r => r.Id == selectedId);
            cases.Add(new("5.4", "Find",
                "residents.Find(r => r.Id == selectedId)",
                visitor.Transpile(expr.Body),
                "result.name === 'Charlie' && result.id === 3"));
        }

        // §5.5 FindIndex
        {
            var targetId = 2;
            Expression<Func<JsArray<Resident>, int>> expr =
                residents => residents.FindIndex(r => r.Id == targetId);
            cases.Add(new("5.5", "FindIndex",
                "residents.FindIndex(r => r.Id == targetId)",
                visitor.Transpile(expr.Body),
                "result === 1"));
        }

        // §5.6 Some
        {
            Expression<Func<JsArray<Resident>, bool>> expr =
                residents => residents.Some(r => r.IsActive);
            cases.Add(new("5.6a", "Some",
                "residents.Some(r => r.IsActive)",
                visitor.Transpile(expr.Body),
                "result === true"));
        }

        // §5.6 Every
        {
            Expression<Func<JsArray<Resident>, bool>> expr =
                residents => residents.Every(r => r.IsActive);
            cases.Add(new("5.6b", "Every",
                "residents.Every(r => r.IsActive)",
                visitor.Transpile(expr.Body),
                "result === false"));
        }

        // §5.7 Reduce
        {
            Expression<Func<JsArray<Resident>, decimal>> expr =
                items => items.Reduce((sum, item) => sum + item.Amount, 0m);
            // Rename 'items' reference for the browser test
            var js = visitor.Transpile(expr.Body);
            cases.Add(new("5.7", "Reduce",
                "items.Reduce((sum, item) => sum + item.Amount, 0)",
                js,
                "result === 750"));
        }

        // §5.8 Sort with Comparer
        {
            Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
                residents => residents.Sort((a, b) => a.Name.LocaleCompare(b.Name));
            cases.Add(new("5.8", "Sort with Comparer",
                "residents.Sort((a, b) => a.Name.LocaleCompare(b.Name))",
                visitor.Transpile(expr.Body),
                "result[0].name === 'Alice' && result[result.length - 1].name === 'Diana'"));
        }

        // §5.9 Filter + Map + Join
        {
            var currentFacility = "F1";
            Expression<Func<JsArray<Resident>, string>> expr =
                residents => residents.Filter(r => r.FacilityId == currentFacility).Map(r => r.Name).Join(", ");
            cases.Add(new("5.9", "Filter + Map + Join",
                "residents.Filter(r => r.FacilityId == currentFacility).Map(r => r.Name).Join(\", \")",
                visitor.Transpile(expr.Body),
                "result === 'Alice, Charlie'"));
        }

        // §5.10 Indexer
        {
            Expression<Func<JsArray<Resident>, Resident>> expr =
                residents => residents[0];
            cases.Add(new("5.10a", "Indexer [0]",
                "residents[0]",
                visitor.Transpile(expr.Body),
                "result.name === 'Alice'"));
        }
        {
            Expression<Func<JsArray<Resident>, Resident>> expr =
                residents => residents[residents.Length - 1];
            cases.Add(new("5.10b", "Indexer [Length - 1]",
                "residents[residents.Length - 1]",
                visitor.Transpile(expr.Body),
                "result.name === 'Diana'"));
        }

        // §5.11 Includes
        {
            var rowIndex = 1;
            Expression<Func<JsArray<int>, bool>> expr =
                selected => selected.Includes(rowIndex);
            cases.Add(new("5.11", "Includes",
                "selected.Includes(rowIndex)",
                visitor.Transpile(expr.Body),
                "result === true"));
        }

        // §5.12 Push
        {
            Expression<Func<JsArray<Resident>, Resident, int>> expr =
                (pending, item) => pending.Push(item);
            cases.Add(new("5.12", "Push",
                "pending.Push(item)",
                visitor.Transpile(expr.Body),
                "result === 1"));
        }

        // §6 ForEach (void lambda)
        {
            Expression<Action<JsArray<Resident>>> expr =
                residents => residents.ForEach(r => JsConsole.Log(r.Name));
            cases.Add(new("6", "ForEach (void lambda)",
                "residents.ForEach(r => JsConsole.Log(r.Name))",
                visitor.Transpile(expr.Body),
                "logged.length === 4 && logged[0] === 'Alice'"));
        }

        return cases;
    }

    public static string GenerateJson()
    {
        var cases = GenerateAll();
        return JsonSerializer.Serialize(cases, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }
}
