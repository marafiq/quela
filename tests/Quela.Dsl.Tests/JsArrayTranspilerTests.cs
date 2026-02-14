using System.Linq.Expressions;
using Quela.Dsl;

namespace Quela.Dsl.Tests;

/// <summary>
/// Test model matching the spec's "resident" domain object.
/// Properties use PascalCase (C#) and should transpile to camelCase (JS).
/// </summary>
public class Resident
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string FacilityId { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>
/// Verifies every spec example from the Shalimar Foundation DSL spec.
/// Each test captures a C# expression tree, transpiles it, and asserts
/// the output matches the expected JavaScript exactly.
/// </summary>
public class JsArrayTranspilerTests
{
    private readonly JsExpressionVisitor _visitor = new();

    private string Transpile(Expression expr) => _visitor.Transpile(expr);

    // ═══ §5.1 Filter ════════════════════════════════════════════════════
    [Fact]
    public void Filter_TranspilesToJsFilter()
    {
        // C#: residents.Filter(r => r.IsActive)
        Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
            residents => residents.Filter(r => r.IsActive);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.filter(function(r) { return r.isActive; })",
            js);
    }

    // ═══ §5.2 Map ═══════════════════════════════════════════════════════
    [Fact]
    public void Map_TranspilesToJsMap()
    {
        // C#: residents.Map(r => r.Name)
        Expression<Func<JsArray<Resident>, JsArray<string>>> expr =
            residents => residents.Map(r => r.Name);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.map(function(r) { return r.name; })",
            js);
    }

    // ═══ §5.3 Filter + Map Chain ════════════════════════════════════════
    [Fact]
    public void FilterMapChain_TranspilesToChainedCalls()
    {
        // C#: residents.Filter(r => r.IsActive).Map(r => r.Name)
        Expression<Func<JsArray<Resident>, JsArray<string>>> expr =
            residents => residents.Filter(r => r.IsActive).Map(r => r.Name);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.filter(function(r) { return r.isActive; }).map(function(r) { return r.name; })",
            js);
    }

    // ═══ §5.4 Find ══════════════════════════════════════════════════════
    [Fact]
    public void Find_WithEquality_TranspilesToTripleEquals()
    {
        // C#: residents.Find(r => r.Id == selectedId)
        var selectedId = 3;
        Expression<Func<JsArray<Resident>, Resident>> expr =
            residents => residents.Find(r => r.Id == selectedId);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.find(function(r) { return r.id === selectedId; })",
            js);
    }

    // ═══ §5.5 FindIndex ═════════════════════════════════════════════════
    [Fact]
    public void FindIndex_TranspilesToJsFindIndex()
    {
        // C#: residents.FindIndex(r => r.Id == targetId)
        var targetId = 2;
        Expression<Func<JsArray<Resident>, int>> expr =
            residents => residents.FindIndex(r => r.Id == targetId);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.findIndex(function(r) { return r.id === targetId; })",
            js);
    }

    // ═══ §5.6 Some / Every ══════════════════════════════════════════════
    [Fact]
    public void Some_TranspilesToJsSome()
    {
        Expression<Func<JsArray<Resident>, bool>> expr =
            residents => residents.Some(r => r.IsActive);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.some(function(r) { return r.isActive; })",
            js);
    }

    [Fact]
    public void Every_TranspilesToJsEvery()
    {
        Expression<Func<JsArray<Resident>, bool>> expr =
            residents => residents.Every(r => r.IsActive);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.every(function(r) { return r.isActive; })",
            js);
    }

    // ═══ §5.7 Reduce ════════════════════════════════════════════════════
    [Fact]
    public void Reduce_TranspilesToJsReduce()
    {
        // C#: items.Reduce((sum, item) => sum + item.Amount, 0)
        Expression<Func<JsArray<Resident>, decimal>> expr =
            items => items.Reduce((sum, item) => sum + item.Amount, 0m);

        var js = Transpile(expr.Body);

        Assert.Equal(
            "items.reduce(function(sum, item) { return sum + item.amount; }, 0)",
            js);
    }

    // ═══ §5.8 Sort with Comparer ════════════════════════════════════════
    [Fact]
    public void SortWithComparer_TranspilesToJsSort()
    {
        // C#: residents.Sort((a, b) => a.Name.LocaleCompare(b.Name))
        Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
            residents => residents.Sort((a, b) => a.Name.LocaleCompare(b.Name));

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.sort(function(a, b) { return a.name.localeCompare(b.name); })",
            js);
    }

    // ═══ §5.9 Filter + Map + Join ═══════════════════════════════════════
    [Fact]
    public void FilterMapJoin_TranspilesToChainedCalls()
    {
        // C#: residents.Filter(r => r.FacilityId == currentFacility).Map(r => r.Name).Join(", ")
        var currentFacility = "F1";
        Expression<Func<JsArray<Resident>, string>> expr =
            residents => residents
                .Filter(r => r.FacilityId == currentFacility)
                .Map(r => r.Name)
                .Join(", ");

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.filter(function(r) { return r.facilityId === currentFacility; }).map(function(r) { return r.name; }).join(', ')",
            js);
    }

    // ═══ §5.10 Indexer ══════════════════════════════════════════════════
    [Fact]
    public void Indexer_TranspilesToBracketAccess()
    {
        // C#: residents[0]
        Expression<Func<JsArray<Resident>, Resident>> expr =
            residents => residents[0];

        var js = Transpile(expr.Body);

        Assert.Equal("residents[0]", js);
    }

    [Fact]
    public void Indexer_LengthMinusOne_TranspilesCorrectly()
    {
        // C#: residents[residents.Length - 1]
        Expression<Func<JsArray<Resident>, Resident>> expr =
            residents => residents[residents.Length - 1];

        var js = Transpile(expr.Body);

        Assert.Equal("residents[residents.length - 1]", js);
    }

    // ═══ §5.11 Includes ═════════════════════════════════════════════════
    [Fact]
    public void Includes_TranspilesToJsIncludes()
    {
        // C#: selected.Includes(rowIndex)
        var rowIndex = 1;
        Expression<Func<JsArray<int>, bool>> expr =
            selected => selected.Includes(rowIndex);

        var js = Transpile(expr.Body);

        Assert.Equal("selected.includes(rowIndex)", js);
    }

    // ═══ §5.12 Mutation ═════════════════════════════════════════════════
    [Fact]
    public void Push_TranspilesToJsPush()
    {
        Expression<Func<JsArray<Resident>, Resident, int>> expr =
            (pending, item) => pending.Push(item);

        var js = Transpile(expr.Body);

        Assert.Equal("pending.push(item)", js);
    }

    [Fact]
    public void Splice_TranspilesToJsSplice()
    {
        Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
            pending => pending.Splice(0, 1);

        var js = Transpile(expr.Body);

        Assert.Equal("pending.splice(0, 1)", js);
    }

    // ═══ §6 ForEach (void lambda — Action) ══════════════════════════════
    [Fact]
    public void ForEach_VoidLambda_EmitsNoReturn()
    {
        // C#: residents.ForEach(r => JsConsole.Log(r.Name))
        Expression<Action<JsArray<Resident>>> expr =
            residents => residents.ForEach(r => JsConsole.Log(r.Name));

        var js = Transpile(expr.Body);

        Assert.Equal(
            "residents.forEach(function(r) { console.log(r.name); })",
            js);
        // Critical: ForEach must NOT contain "return"
        Assert.DoesNotContain("return", js);
    }

    // ═══ Additional spec rules ══════════════════════════════════════════

    [Fact]
    public void EqualityOperator_CSharpDoubleEquals_BecomesTripleEquals()
    {
        // The spec requires C# == to become JS ===
        var id = 5;
        Expression<Func<JsArray<Resident>, Resident>> expr =
            residents => residents.Find(r => r.Id == id);

        var js = Transpile(expr.Body);

        Assert.Contains("===", js);
        Assert.DoesNotContain("==", js.Replace("===", ""));
    }

    [Fact]
    public void InequalityOperator_CSharpNotEquals_BecomesStrictNotEquals()
    {
        var id = 5;
        Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
            residents => residents.Filter(r => r.Id != id);

        var js = Transpile(expr.Body);

        Assert.Contains("!==", js);
    }

    [Fact]
    public void PascalCase_Properties_BecomeCamelCase()
    {
        Expression<Func<JsArray<Resident>, JsArray<string>>> expr =
            residents => residents.Map(r => r.FacilityId);

        var js = Transpile(expr.Body);

        Assert.Contains("r.facilityId", js);
        Assert.DoesNotContain("FacilityId", js);
    }

    [Fact]
    public void Length_Property_TranspilesToLength()
    {
        Expression<Func<JsArray<Resident>, int>> expr =
            residents => residents.Length;

        var js = Transpile(expr.Body);

        Assert.Equal("residents.length", js);
    }

    [Fact]
    public void IndexOf_TranspilesToJsIndexOf()
    {
        Expression<Func<JsArray<int>, int, int>> expr =
            (arr, item) => arr.IndexOf(item);

        var js = Transpile(expr.Body);

        Assert.Equal("arr.indexOf(item)", js);
    }

    [Fact]
    public void Slice_TranspilesToJsSlice()
    {
        Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
            residents => residents.Slice(1, 3);

        var js = Transpile(expr.Body);

        Assert.Equal("residents.slice(1, 3)", js);
    }

    [Fact]
    public void Concat_TranspilesToJsConcat()
    {
        Expression<Func<JsArray<Resident>, JsArray<Resident>, JsArray<Resident>>> expr =
            (a, b) => a.Concat(b);

        var js = Transpile(expr.Body);

        Assert.Equal("a.concat(b)", js);
    }

    [Fact]
    public void Reverse_TranspilesToJsReverse()
    {
        Expression<Func<JsArray<Resident>, JsArray<Resident>>> expr =
            residents => residents.Reverse();

        var js = Transpile(expr.Body);

        Assert.Equal("residents.reverse()", js);
    }

    // ═══ Multi-statement block tests (JsBlockBuilder) ═══════════════════

    [Fact]
    public void BlockBuilder_FilterAndAssign_ProducesMultilineJs()
    {
        // §5.1 full example:
        // var active = residents.Filter(r => r.IsActive);
        // residentGrid.dataSource = active;
        Expression<Func<JsArray<Resident>, JsArray<Resident>>> filterExpr =
            residents => residents.Filter(r => r.IsActive);

        var block = new JsBlockBuilder()
            .Var("active", filterExpr.Body)
            .Raw("residentGrid.dataSource = active;");

        var js = block.Build();

        Assert.Equal(
            "var active = residents.filter(function(r) { return r.isActive; });\n" +
            "residentGrid.dataSource = active;",
            js);
    }

    [Fact]
    public void BlockBuilder_ReduceAndAssign_ProducesMultilineJs()
    {
        // §5.7 full example
        Expression<Func<JsArray<Resident>, decimal>> reduceExpr =
            items => items.Reduce((sum, item) => sum + item.Amount, 0m);

        var block = new JsBlockBuilder()
            .Var("total", reduceExpr.Body)
            .Raw("statusLabel.innerText = 'Total: $' + total;");

        var js = block.Build();

        Assert.Equal(
            "var total = items.reduce(function(sum, item) { return sum + item.amount; }, 0);\n" +
            "statusLabel.innerText = 'Total: $' + total;",
            js);
    }
}
