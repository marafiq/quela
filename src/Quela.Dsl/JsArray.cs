using System.Linq.Expressions;

namespace Quela.Dsl;

/// <summary>
/// Phantom type for JavaScript Array. Methods are never called at runtime —
/// they exist only so C# expression trees can capture the intent, which the
/// <see cref="JsExpressionVisitor"/> then emits as JavaScript.
/// </summary>
[JsType("Array")]
public sealed class JsArray<T>
{
    // Prevent construction — arrays come from components, methods, or Fetch
    private JsArray() { }

    // ── Properties ──────────────────────────────────────────
    [JsEmit("length")]
    public int Length => throw new PhantomTypeException();

    // ── Iteration (lambda arguments) ────────────────────────
    [JsEmit("filter")]
    public JsArray<T> Filter(Expression<Func<T, bool>> predicate) => throw new PhantomTypeException();

    [JsEmit("map")]
    public JsArray<TOut> Map<TOut>(Expression<Func<T, TOut>> selector) => throw new PhantomTypeException();

    [JsEmit("find")]
    public T Find(Expression<Func<T, bool>> predicate) => throw new PhantomTypeException();

    [JsEmit("findIndex")]
    public int FindIndex(Expression<Func<T, bool>> predicate) => throw new PhantomTypeException();

    [JsEmit("some")]
    public bool Some(Expression<Func<T, bool>> predicate) => throw new PhantomTypeException();

    [JsEmit("every")]
    public bool Every(Expression<Func<T, bool>> predicate) => throw new PhantomTypeException();

    [JsEmit("forEach")]
    public void ForEach(Expression<Action<T>> action) => throw new PhantomTypeException();

    [JsEmit("reduce")]
    public TOut Reduce<TOut>(Expression<Func<TOut, T, TOut>> reducer, TOut initial) => throw new PhantomTypeException();

    // ── Lookup (no lambda) ──────────────────────────────────
    [JsEmit("indexOf")]
    public int IndexOf(T item) => throw new PhantomTypeException();

    [JsEmit("includes")]
    public bool Includes(T item) => throw new PhantomTypeException();

    [JsEmit("join")]
    public string Join(string separator) => throw new PhantomTypeException();

    // ── Slicing & combining ─────────────────────────────────
    [JsEmit("slice")]
    public JsArray<T> Slice(int start) => throw new PhantomTypeException();

    [JsEmit("slice")]
    public JsArray<T> Slice(int start, int end) => throw new PhantomTypeException();

    [JsEmit("concat")]
    public JsArray<T> Concat(JsArray<T> other) => throw new PhantomTypeException();

    [JsEmit("reverse")]
    public JsArray<T> Reverse() => throw new PhantomTypeException();

    // ── Sorting ─────────────────────────────────────────────
    [JsEmit("sort")]
    public JsArray<T> Sort() => throw new PhantomTypeException();

    [JsEmit("sort")]
    public JsArray<T> Sort(Expression<Func<T, T, int>> comparer) => throw new PhantomTypeException();

    // ── Mutation ────────────────────────────────────────────
    [JsEmit("push")]
    public int Push(T item) => throw new PhantomTypeException();

    [JsEmit("pop")]
    public T Pop() => throw new PhantomTypeException();

    [JsEmit("shift")]
    public T Shift() => throw new PhantomTypeException();

    [JsEmit("unshift")]
    public int Unshift(T item) => throw new PhantomTypeException();

    [JsEmit("splice")]
    public JsArray<T> Splice(int start, int deleteCount) => throw new PhantomTypeException();

    [JsEmit("splice")]
    public JsArray<T> Splice(int start, int deleteCount, T item) => throw new PhantomTypeException();

    // ── Indexer ─────────────────────────────────────────────
    public T this[int index]
    {
        [JsEmit("{index}")]
        get => throw new PhantomTypeException();
        [JsEmit("{index}")]
        set => throw new PhantomTypeException();
    }
}

public sealed class PhantomTypeException : InvalidOperationException
{
    public PhantomTypeException()
        : base("Phantom types cannot be invoked at runtime. Use the expression visitor to transpile to JavaScript.") { }
}
