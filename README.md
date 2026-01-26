# Quela - Type-Safe SQL DSL for .NET

> **Query Language for .NET** — Write SQL with compile-time safety

```csharp
using Quela;
using static MyApp.Database.Db;

var query = Sql
    .From(Residents)
    .Join(Units).On(Residents.UnitId == Units.Id)
    .Where(Residents.Status == "Active")
    .Where(Residents.FacilityId == facilityId)
    .OrderBy(Residents.LastName.Asc())
    .Select(Residents.Id, Residents.FullName, Units.Name);

var results = await query.QueryAsync<ResidentDto>(connection);
```

## What makes Quela different

- **100% SQL Server syntax coverage** — Window functions, CTEs, PIVOT, temporal tables, everything
- **Compile-time safety** — Invalid SQL won't compile
- **Source-generated schema** — `Db.Residents.Id` not `"Residents.Id"`
- **Zero abstraction leak** — You write SQL, you get SQL

## Installation

```bash
dotnet add package Quela
```

## Quick Start

### Simple Query

```csharp
var query = Sql
    .From(Products)
    .Where(Products.Price > 100)
    .OrderBy(Products.Name.Asc())
    .Select(Products.Id, Products.Name, Products.Price);

var results = await query.QueryAsync<ProductDto>(connection);
```

### Join Query

```csharp
var query = Sql
    .From(Products)
    .LeftJoin(Categories).On(Products.CategoryId == Categories.Id)
    .Where(Products.Price > 50)
    .And(Categories.Name.IsNotNull())
    .OrderBy(Products.Price.Desc())
    .Select(Products.Name, Products.Price, Categories.Name.As("CategoryName"));
```

### Aggregation with Group By

```csharp
var query = Sql
    .From(Products)
    .Join(Categories).On(Products.CategoryId == Categories.Id)
    .GroupBy(Categories.Name)
    .Having(Fn.Count() > 5)
    .Select(Categories.Name, Fn.Count().As("ProductCount"), Fn.Avg(Products.Price).As("AvgPrice"));
```

### Window Functions

```csharp
var query = Sql
    .From(Products)
    .Select(
        Products.Id,
        Products.Name,
        Products.Price,
        Window.RowNumber()
              .Over(o => o.PartitionBy(Products.CategoryId)
                          .OrderBy(Products.Price.Desc()))
              .As("Rank"));
```

### Common Table Expressions (CTEs)

```csharp
var query = Sql
    .With("ExpensiveProducts", () => Sql
        .From(Products)
        .Where(Products.Price > 1000)
        .Select(Products.Id, Products.Name, Products.Price))
    .From(Sql.Cte("ExpensiveProducts"))
    .SelectAll();
```

### CROSS APPLY with Typed Alias

```csharp
public class OrderTotals : TypedAlias
{
    public OrderTotals(string alias = "t") : base(alias) { }

    public Column<decimal> Total => Col<decimal>();
    public Column<int> ItemCount => Col<int>();
}

var t = new OrderTotals();

var query = Sql
    .From(Orders)
    .CrossApply(
        Sql.From(OrderItems)
           .Where(OrderItems.OrderId == Orders.Id)
           .Select(Fn.Sum(OrderItems.Amount).As("Total"), Fn.Count().As("ItemCount")),
        t)
    .Where(t.Total > 1000)
    .Select(Orders.Id, t.Total, t.ItemCount);
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Layer 2: CONVENIENCE PATTERNS (optional)                      │
│  QueryContext, TypedView, WhereIf                              │
└─────────────────────────────────────────────────────────────────┘
                              ↑
┌─────────────────────────────────────────────────────────────────┐
│  Layer 1: TYPED OUTPUTS                                        │
│  TypedAlias for CROSS APPLY                                    │
└─────────────────────────────────────────────────────────────────┘
                              ↑
┌─────────────────────────────────────────────────────────────────┐
│  Layer 0: CORE DSL (complete, standalone)                      │
│  Sql.From().Join().Where().Select()                            │
│  Table, Column<T>, Condition, IQuery                           │
└─────────────────────────────────────────────────────────────────┘
```

## What This Prevents at Compile Time

| Invalid SQL                   | Compile Error                                              |
|-------------------------------|-----------------------------------------------------------|
| `.Select()` before `.From()`  | `Sql` has no `.Select()` method                           |
| `.Join()` without `.On()`     | `IJoin<T>` only has `.On()` method                        |
| `.Where()` after `.GroupBy()` | `IHaving<T>` has no `.Where()` method                     |
| `Products.Price > "hello"`    | Type mismatch: `Column<decimal>` vs `string`              |
| Typo in column name           | No string column names — use generated `Db.Products.Price`|

## SQL Functions

### Aggregates
```csharp
Fn.Count()                    // COUNT(*)
Fn.Count(col)                 // COUNT(col)
Fn.CountDistinct(col)         // COUNT(DISTINCT col)
Fn.Sum(col)                   // SUM(col)
Fn.Avg(col)                   // AVG(col)
Fn.Min(col)                   // MIN(col)
Fn.Max(col)                   // MAX(col)
```

### Window Functions
```csharp
Window.RowNumber()            // ROW_NUMBER()
Window.Rank()                 // RANK()
Window.DenseRank()            // DENSE_RANK()
Window.Lag(col)               // LAG(col)
Window.Lead(col)              // LEAD(col)
Window.FirstValue(col)        // FIRST_VALUE(col)
```

### String Functions
```csharp
Fn.Len(col)                   // LEN(col)
Fn.Upper(col)                 // UPPER(col)
Fn.Lower(col)                 // LOWER(col)
Fn.Substring(col, 1, 10)      // SUBSTRING(col, 1, 10)
col.Like("pattern%")          // col LIKE 'pattern%'
col.Contains("text")          // col LIKE '%text%'
```

### Date Functions
```csharp
Fn.GetDate()                  // GETDATE()
Fn.Year(col)                  // YEAR(col)
Fn.DateAdd(DatePart.Day, 7, col)  // DATEADD(DAY, 7, col)
```

## License

MIT
