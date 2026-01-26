using Quela;
using Quela.Dml;
using static Quela.Tests.Db;

namespace Quela.Tests;

/// <summary>
/// Comprehensive test runner for Quela SQL DSL.
/// Tests SQL generation and validates syntax.
/// </summary>
class Program
{
    static int _passed = 0;
    static int _failed = 0;
    static bool _verbose = false;

    static void Main(string[] args)
    {
        _verbose = args.Contains("--verbose") || args.Contains("-v");

        Console.WriteLine("Quela SQL DSL - Comprehensive Test Suite");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 1: Basic SELECT Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("── Basic SELECT Tests ──");

        RunTest("SimpleSelect_GeneratesValidSql", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Id, Products.Name, Products.Price);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "SELECT", "[Products].[Id]", "[Products].[Name]", "FROM [dbo].[Products]");
        });

        RunTest("SelectAll_GeneratesStarSyntax", () =>
        {
            var query = Sql.From(ProductsTable).SelectAll();
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "SELECT *", "FROM [dbo].[Products]");
        });

        RunTest("SelectWithAlias_GeneratesAsSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Name.As("ProductName"), Products.Price.As("UnitPrice"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "[Products].[Name] AS [ProductName]", "[Products].[Price] AS [UnitPrice]");
        });

        RunTest("SelectDistinct_GeneratesDistinctKeyword", () =>
        {
            var query = Sql.From(ProductsTable).SelectDistinct(Products.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "SELECT DISTINCT");
        });

        RunTest("SelectTop_GeneratesTopClause", () =>
        {
            var query = Sql.From(ProductsTable).SelectTop(10, Products.Id, Products.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "SELECT TOP (10)");
        });

        RunTest("SelectTopWithDistinct_CombinesBothKeywords", () =>
        {
            var query = Sql.From(ProductsTable).SelectTop(5, Products.CategoryId);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "TOP (5)");
        });

        RunTest("SelectFromMultipleTables_GeneratesCommaSeparatedFrom", () =>
        {
            var query = Sql.From(ProductsTable, CategoriesTable).SelectAll();
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "[dbo].[Products]", "[dbo].[Categories]");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 2: WHERE Clause Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── WHERE Clause Tests ──");

        RunTest("WhereEquals_ParameterizesValue", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Name == "Widget").Select(Products.Id);
            var result = query.Build();
            SqlValidator.AssertValid(result.Sql);
            SqlValidator.AssertContains(result.Sql, "WHERE [Products].[Name] = @p0");
            Assert(result.Parameters["@p0"]?.ToString() == "Widget", "Parameter value mismatch");
        });

        RunTest("WhereNotEquals_GeneratesNotEqualsSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Status != "Deleted").Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "<>");
        });

        RunTest("WhereGreaterThan_GeneratesComparisonOperator", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Price > 100m).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "[Products].[Price] > @p0");
        });

        RunTest("WhereLessThan_GeneratesComparisonOperator", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Price < 50m).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "[Products].[Price] < @p0");
        });

        RunTest("WhereGreaterThanOrEqual_GeneratesComparisonOperator", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Price >= 100m).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "[Products].[Price] >= @p0");
        });

        RunTest("WhereLessThanOrEqual_GeneratesComparisonOperator", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Price <= 50m).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "[Products].[Price] <= @p0");
        });

        RunTest("WhereIsNull_GeneratesIsNullSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.NullableCategoryId.IsNull()).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "IS NULL");
        });

        RunTest("WhereIsNotNull_GeneratesIsNotNullSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.NullableCategoryId.IsNotNull()).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "IS NOT NULL");
        });

        RunTest("WhereAnd_CombinesConditions", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .And(Products.Status == "Active")
                .Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "AND");
        });

        RunTest("WhereOr_CombinesConditions", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Price > 1000)
                .Or(Products.Status == "Featured")
                .Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "OR");
        });

        RunTest("WhereComplexCondition_GroupsCorrectly", () =>
        {
            var condition = (Products.Price > 100) & (Products.Status == "Active");
            var query = Sql.From(ProductsTable).Where(condition).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "AND");
        });

        RunTest("WhereBetween_GeneratesBetweenSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Price.Between(10m, 100m)).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "BETWEEN", "AND");
        });

        RunTest("WhereLike_GeneratesLikeSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Name.Like("Widget%")).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "LIKE");
        });

        RunTest("WhereNotLike_GeneratesNotLikeSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Name.NotLike("%test%")).Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "NOT LIKE");
        });

        RunTest("WhereIn_GeneratesInListSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Status.In("Active", "Pending", "Review"))
                .Select(Products.Id);
            var result = query.Build();
            SqlValidator.AssertValid(result.Sql);
            SqlValidator.AssertContains(result.Sql, "IN (");
            Assert(result.Parameters.Count == 3, "Should have 3 parameters for IN list");
        });

        RunTest("WhereNotIn_GeneratesNotInListSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Status.NotIn("Deleted", "Archived"))
                .Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "NOT IN (");
        });

        RunTest("WhereInEmptyList_GeneratesFalseCondition", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Status.In(Array.Empty<string>()))
                .Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertContains(sql, "1 = 0");
        });

        RunTest("WhereNotInEmptyList_GeneratesTrueCondition", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Status.NotIn(Array.Empty<string>()))
                .Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertContains(sql, "1 = 1");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 3: JOIN Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── JOIN Tests ──");

        RunTest("InnerJoin_GeneratesJoinSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Join(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "INNER JOIN", "ON");
        });

        RunTest("LeftOuterJoin_GeneratesLeftJoinSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .LeftJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "LEFT OUTER JOIN");
        });

        RunTest("RightOuterJoin_GeneratesRightJoinSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .RightJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "RIGHT OUTER JOIN");
        });

        RunTest("FullOuterJoin_GeneratesFullJoinSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .FullJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "FULL OUTER JOIN");
        });

        RunTest("CrossJoin_GeneratesCrossJoinSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .CrossJoin(CategoriesTable)
                .Select(Products.Name, Categories.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "CROSS JOIN");
        });

        RunTest("MultipleJoins_GeneratesAllJoinClauses", () =>
        {
            var query = Sql.From(OrdersTable)
                .Join(CustomersTable).On(Orders.CustomerId == Customers.Id)
                .Join(OrderItemsTable).On(OrderItems.OrderId == Orders.Id)
                .Join(ProductsTable).On(OrderItems.ProductId == Products.Id)
                .Select(Orders.Id, Customers.Name, Products.Name, OrderItems.Amount);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            Assert(sql.Split("INNER JOIN").Length - 1 == 3, "Should have 3 INNER JOINs");
        });

        RunTest("JoinWithComplexCondition_GeneratesComplexOnClause", () =>
        {
            var query = Sql.From(OrdersTable)
                .Join(CustomersTable).On((Orders.CustomerId == Customers.Id) & (Customers.Status == "Active"))
                .Select(Orders.Id, Customers.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "ON", "AND");
        });

        RunTest("JoinSubquery_GeneratesSubqueryJoin", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name, Products.CategoryId);
            var query = Sql.From(CategoriesTable)
                .Join(subquery, "ExpensiveProducts").On(Categories.Id == Sql.Col<int>("ExpensiveProducts.CategoryId"))
                .Select(Categories.Name, Sql.Col<string>("ExpensiveProducts.Name"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "INNER JOIN (SELECT", "AS [ExpensiveProducts]");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 4: ORDER BY Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── ORDER BY Tests ──");

        RunTest("OrderByAsc_GeneratesAscSyntax", () =>
        {
            var query = Sql.From(ProductsTable).OrderBy(Products.Name.Asc()).Select(Products.Id, Products.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "ORDER BY [Products].[Name] ASC");
        });

        RunTest("OrderByDesc_GeneratesDescSyntax", () =>
        {
            var query = Sql.From(ProductsTable).OrderBy(Products.Price.Desc()).Select(Products.Id, Products.Price);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "ORDER BY [Products].[Price] DESC");
        });

        RunTest("OrderByMultipleColumns_GeneratesCommaSeparated", () =>
        {
            var query = Sql.From(ProductsTable)
                .OrderBy(Products.CategoryId.Asc(), Products.Price.Desc())
                .Select(Products.Id, Products.CategoryId, Products.Price);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "ORDER BY", "ASC", "DESC");
        });

        RunTest("OffsetFetch_GeneratesPaginationSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .OrderBy(Products.Id.Asc())
                .Select(Products.Id, Products.Name)
                .OffsetFetch(10, 20);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY");
        });

        RunTest("OffsetOnly_GeneratesOffsetWithoutFetch", () =>
        {
            var query = Sql.From(ProductsTable)
                .OrderBy(Products.Id.Asc())
                .Select(Products.Id, Products.Name)
                .Offset(10);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "OFFSET 10 ROWS");
            SqlValidator.AssertNotContains(sql, "FETCH");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 5: Aggregate Function Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Aggregate Function Tests ──");

        RunTest("CountStar_GeneratesCountStarSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.Count());
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "COUNT(*)");
        });

        RunTest("CountColumn_GeneratesCountColumnSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.Count(Products.CategoryId));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "COUNT([Products].[CategoryId])");
        });

        RunTest("CountDistinct_GeneratesCountDistinctSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.CountDistinct(Products.CategoryId));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "COUNT(DISTINCT [Products].[CategoryId])");
        });

        RunTest("Sum_GeneratesSumSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.Sum(Products.Price));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "SUM([Products].[Price])");
        });

        RunTest("Avg_GeneratesAvgSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.Avg(Products.Price));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "AVG([Products].[Price])");
        });

        RunTest("Min_GeneratesMinSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.Min(Products.Price));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "MIN([Products].[Price])");
        });

        RunTest("Max_GeneratesMaxSyntax", () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.Max(Products.Price));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "MAX([Products].[Price])");
        });

        RunTest("GroupBy_GeneratesGroupBySyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .GroupBy(Products.CategoryId)
                .Select(Products.CategoryId, Fn.Count());
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "GROUP BY [Products].[CategoryId]");
        });

        RunTest("GroupByMultipleColumns_GeneratesMultipleGroupBy", () =>
        {
            var query = Sql.From(ProductsTable)
                .GroupBy(Products.CategoryId, Products.Status)
                .Select(Products.CategoryId, Products.Status, Fn.Count());
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "GROUP BY [Products].[CategoryId], [Products].[Status]");
        });

        RunTest("Having_GeneratesHavingSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .GroupBy(Products.CategoryId)
                .Having(Fn.Count() > 5)
                .Select(Products.CategoryId, Fn.Count());
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "HAVING COUNT(*) >");
        });

        RunTest("HavingWithSum_GeneratesHavingWithAggregate", () =>
        {
            var query = Sql.From(OrderItemsTable)
                .GroupBy(OrderItems.OrderId)
                .Having(Fn.Sum(OrderItems.Amount) > 1000)
                .Select(OrderItems.OrderId, Fn.Sum(OrderItems.Amount));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "HAVING SUM([OrderItems].[Amount]) >");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 6: Window Function Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Window Function Tests ──");

        RunTest("RowNumber_GeneratesRowNumberSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Id, Window.RowNumber().Over(o => o.OrderBy(Products.Price.Desc())).As("RowNum"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "ROW_NUMBER()", "OVER", "ORDER BY");
        });

        RunTest("RowNumberWithPartition_GeneratesPartitionSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Id,
                    Window.RowNumber()
                        .Over(o => o.PartitionBy(Products.CategoryId).OrderBy(Products.Price.Desc()))
                        .As("RowNum"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "PARTITION BY [Products].[CategoryId]");
        });

        RunTest("Rank_GeneratesRankSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Id, Window.Rank().Over(o => o.OrderBy(Products.Price.Desc())).As("Rank"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "RANK()", "OVER");
        });

        RunTest("DenseRank_GeneratesDenseRankSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Id, Window.DenseRank().Over(o => o.OrderBy(Products.Price.Desc())).As("DenseRank"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "DENSE_RANK()", "OVER");
        });

        RunTest("Lag_GeneratesLagSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Id, Products.Price,
                    Window.Lag(Products.Price, 1).Over(o => o.OrderBy(Products.Id.Asc())).As("PrevPrice"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "LAG([Products].[Price], 1)", "OVER");
        });

        RunTest("Lead_GeneratesLeadSyntax", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Id, Products.Price,
                    Window.Lead(Products.Price, 1).Over(o => o.OrderBy(Products.Id.Asc())).As("NextPrice"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "LEAD([Products].[Price], 1)", "OVER");
        });

        RunTest("SumOver_GeneratesSumWindowSyntax", () =>
        {
            var query = Sql.From(OrderItemsTable)
                .Select(OrderItems.OrderId, OrderItems.Amount,
                    Window.Sum(OrderItems.Amount)
                        .Over(o => o.PartitionBy(OrderItems.OrderId))
                        .As("OrderTotal"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "SUM([OrderItems].[Amount]) OVER");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 7: CTE Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── CTE Tests ──");

        RunTest("SimpleCte_GeneratesWithClause", () =>
        {
            var query = Sql.With("ExpensiveProducts", () =>
                    Sql.From(ProductsTable).Where(Products.Price > 1000).Select(Products.Id, Products.Name, Products.Price))
                .From(Sql.Cte("ExpensiveProducts"))
                .SelectAll();
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "WITH [ExpensiveProducts] AS", "FROM [ExpensiveProducts]");
        });

        RunTest("MultipleCtes_GeneratesMultipleWithClauses", () =>
        {
            var query = Sql
                .With("Expensive", () => Sql.From(ProductsTable).Where(Products.Price > 1000).Select(Products.Id, Products.CategoryId))
                .With("Popular", () => Sql.From(OrderItemsTable).GroupBy(OrderItems.ProductId).Having(Fn.Count() > 10).Select(OrderItems.ProductId))
                .From(Sql.Cte("Expensive"))
                .Join(Sql.Cte("Popular")).On(Sql.Col<int>("Expensive.Id") == Sql.Col<int>("Popular.ProductId"))
                .SelectAll();
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "WITH", "[Expensive] AS", "[Popular] AS");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 8: Set Operation Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Set Operation Tests ──");

        RunTest("Union_GeneratesUnionSyntax", () =>
        {
            var q1 = Sql.From(ProductsTable).Where(Products.Price > 100).Select(Products.Id, Products.Name);
            var q2 = Sql.From(ProductsTable).Where(Products.Status == "Featured").Select(Products.Id, Products.Name);
            var combined = q1.Union(q2).Select(Sql.All);
            var sql = combined.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "UNION");
            SqlValidator.AssertNotContains(sql, "UNION ALL");
        });

        RunTest("UnionAll_GeneratesUnionAllSyntax", () =>
        {
            var q1 = Sql.From(ProductsTable).Where(Products.Price > 100).Select(Products.Id, Products.Name);
            var q2 = Sql.From(ProductsTable).Where(Products.Status == "Featured").Select(Products.Id, Products.Name);
            var combined = q1.UnionAll(q2).Select(Sql.All);
            var sql = combined.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "UNION ALL");
        });

        RunTest("Intersect_GeneratesIntersectSyntax", () =>
        {
            var q1 = Sql.From(ProductsTable).Where(Products.Price > 100).Select(Products.Id);
            var q2 = Sql.From(ProductsTable).Where(Products.Status == "Active").Select(Products.Id);
            var combined = q1.Intersect(q2).Select(Sql.All);
            var sql = combined.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "INTERSECT");
        });

        RunTest("Except_GeneratesExceptSyntax", () =>
        {
            var q1 = Sql.From(ProductsTable).Select(Products.Id);
            var q2 = Sql.From(OrderItemsTable).Select(OrderItems.ProductId);
            var combined = q1.Except(q2).Select(Sql.All);
            var sql = combined.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "EXCEPT");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 9: Subquery Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Subquery Tests ──");

        RunTest("InSubquery_GeneratesInSelectSyntax", () =>
        {
            var subquery = Sql.From(OrderItemsTable).Select(OrderItems.ProductId);
            var query = Sql.From(ProductsTable).Where(Products.Id.In(subquery)).Select(Products.Id, Products.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "IN (SELECT");
        });

        RunTest("ExistsSubquery_GeneratesExistsSyntax", () =>
        {
            var subquery = Sql.From(OrderItemsTable).Where(OrderItems.ProductId == Products.Id).Select(Sql.Literal(1));
            var query = Sql.From(ProductsTable).Where(Sql.Exists(subquery)).Select(Products.Id, Products.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "EXISTS (SELECT");
        });

        RunTest("NotExistsSubquery_GeneratesNotExistsSyntax", () =>
        {
            var subquery = Sql.From(OrderItemsTable).Where(OrderItems.ProductId == Products.Id).Select(Sql.Literal(1));
            var query = Sql.From(ProductsTable).Where(Sql.NotExists(subquery)).Select(Products.Id, Products.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "NOT EXISTS (SELECT");
        });

        RunTest("SubqueryInFrom_GeneratesDerivedTable", () =>
        {
            var subquery = Sql.From(ProductsTable).Where(Products.Price > 100).Select(Products.Id, Products.Name, Products.CategoryId);
            var query = Sql.From(subquery, "ExpensiveProducts")
                .Join(CategoriesTable).On(Sql.Col<int>("ExpensiveProducts.CategoryId") == Categories.Id)
                .Select(Sql.Col<string>("ExpensiveProducts.Name"), Categories.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "FROM (SELECT", "AS [ExpensiveProducts]");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 10: CASE Expression Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── CASE Expression Tests ──");

        RunTest("CaseWhen_GeneratesCaseSyntax", () =>
        {
            var caseExpr = Case.When(Products.Price > 1000, "Expensive")
                               .When(Products.Price > 100, "Medium")
                               .Else("Cheap");
            var query = Sql.From(ProductsTable).Select(Products.Name, caseExpr.As("PriceCategory"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "CASE", "WHEN", "THEN", "ELSE", "END");
        });

        RunTest("CaseWhenWithoutElse_GeneratesCaseSyntax", () =>
        {
            var caseExpr = Case.When(Products.Status == "Active", "Yes").End();
            var query = Sql.From(ProductsTable).Select(Products.Name, caseExpr.As("IsActive"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "CASE", "WHEN", "THEN", "END");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 11: Literal & Raw SQL Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Literal & Raw SQL Tests ──");

        RunTest("LiteralInt_GeneratesLiteralValue", () =>
        {
            var query = Sql.From(ProductsTable).Select(Products.Name, Sql.Literal(1).As("One"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "1 AS [One]");
        });

        RunTest("LiteralString_GeneratesQuotedLiteral", () =>
        {
            var query = Sql.From(ProductsTable).Select(Products.Name, Sql.Literal("test").As("Test"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "'test' AS [Test]");
        });

        RunTest("RawSql_AllowsCustomSqlExpression", () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(Products.Name, Sql.Raw<DateTime>($"GETDATE()").As("CurrentTime"));
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "GETDATE()");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 12: Parameterization Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Parameterization Tests ──");

        RunTest("MultipleParameters_NumberedSequentially", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Name == "Widget")
                .And(Products.Price > 100)
                .And(Products.Status == "Active")
                .Select(Products.Id);
            var result = query.Build();
            Assert(result.Parameters.ContainsKey("@p0"), "Should have @p0");
            Assert(result.Parameters.ContainsKey("@p1"), "Should have @p1");
            Assert(result.Parameters.ContainsKey("@p2"), "Should have @p2");
        });

        RunTest("NullParameter_GeneratesIsNullNotParameter", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Name == (string?)null).Select(Products.Id);
            var result = query.Build();
            SqlValidator.AssertContains(result.Sql, "IS NULL");
            Assert(result.Parameters.Count == 0, "Should have no parameters for null comparison");
        });

        RunTest("UnicodeString_ParameterizedCorrectly", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Name == "日本語テスト").Select(Products.Id);
            var result = query.Build();
            Assert(result.Parameters["@p0"]?.ToString() == "日本語テスト", "Unicode should be preserved");
        });

        RunTest("SpecialCharacters_ParameterizedCorrectly", () =>
        {
            var query = Sql.From(ProductsTable).Where(Products.Name == "O'Brien; DROP TABLE--").Select(Products.Id);
            var result = query.Build();
            SqlValidator.AssertContains(result.Sql, "@p0");
            SqlValidator.AssertNotContains(result.Sql, "DROP TABLE");
            Assert(result.Parameters["@p0"]?.ToString() == "O'Brien; DROP TABLE--", "Special chars should be in parameter");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 13: Edge Cases & Complex Queries
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Edge Cases & Complex Queries ──");

        RunTest("ComplexQuery_AllClausesCombined", () =>
        {
            var query = Sql.From(OrdersTable)
                .Join(CustomersTable).On(Orders.CustomerId == Customers.Id)
                .Join(OrderItemsTable).On(OrderItems.OrderId == Orders.Id)
                .Join(ProductsTable).On(OrderItems.ProductId == Products.Id)
                .Where(Orders.OrderDate > DateTime.Now.AddDays(-30))
                .And(Products.Status == "Active")
                .GroupBy(Customers.Name, Products.CategoryId)
                .Having(Fn.Sum(OrderItems.Amount) > 1000)
                .OrderBy(Fn.Sum(OrderItems.Amount).Desc())
                .Select(Customers.Name, Products.CategoryId, Fn.Sum(OrderItems.Amount).As("TotalAmount"))
                .OffsetFetch(0, 10);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "SELECT", "FROM", "INNER JOIN", "WHERE", "GROUP BY", "HAVING", "ORDER BY", "OFFSET", "FETCH");
        });

        RunTest("NestedSubqueries_GeneratesCorrectly", () =>
        {
            var innerSubquery = Sql.From(OrderItemsTable)
                .GroupBy(OrderItems.ProductId)
                .Having(Fn.Count() > 5)
                .Select(OrderItems.ProductId);
            var outerSubquery = Sql.From(ProductsTable)
                .Where(Products.Id.In(innerSubquery))
                .Select(Products.CategoryId);
            var query = Sql.From(CategoriesTable)
                .Where(Categories.Id.In(outerSubquery))
                .Select(Categories.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            Assert(sql.Split("SELECT").Length - 1 >= 3, "Should have at least 3 SELECT statements");
        });

        RunTest("EmptyConditions_HandledGracefully", () =>
        {
            var query = Sql.From(ProductsTable).Select(Products.Id, Products.Name);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertNotContains(sql, "WHERE");
        });

        RunTest("ChainedConditions_MaintainsCorrectPrecedence", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .And(Products.Status == "Active")
                .Or(Products.Status == "Featured")
                .Select(Products.Id);
            var sql = query.ToSql();
            SqlValidator.AssertValid(sql);
            SqlValidator.AssertContains(sql, "WHERE", "AND", "OR");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 14: SQL Injection Prevention Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── SQL Injection Prevention Tests ──");

        RunTest("SqlInjection_InStringValue_Parameterized", () =>
        {
            var malicious = "'; DELETE FROM Products; --";
            var query = Sql.From(ProductsTable).Where(Products.Name == malicious).Select(Products.Id);
            var result = query.Build();
            SqlValidator.AssertNotContains(result.Sql, "DELETE");
            SqlValidator.AssertContains(result.Sql, "@p0");
        });

        RunTest("SqlInjection_InLikePattern_Parameterized", () =>
        {
            var malicious = "%'; DELETE FROM Products; --%";
            var query = Sql.From(ProductsTable).Where(Products.Name.Like(malicious)).Select(Products.Id);
            var result = query.Build();
            SqlValidator.AssertNotContains(result.Sql, "DELETE");
            SqlValidator.AssertContains(result.Sql, "@p0");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 15: INSERT Statement Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── INSERT Statement Tests ──");

        RunTest("Insert_SingleRow_GeneratesValidSql", () =>
        {
            var insert = Sql.InsertInto(ProductsTable)
                .Columns(Products.Name, Products.Price, Products.CategoryId)
                .Values("Widget", 99.99m, 1);
            var result = insert.Build();
            AssertContainsDml(result.Sql, "INSERT INTO [dbo].[Products]", "([Name], [Price], [CategoryId])", "VALUES", "@p0", "@p1", "@p2");
            Assert(result.Parameters.Count == 3, "Should have 3 parameters");
        });

        RunTest("Insert_MultipleRows_GeneratesBulkInsert", () =>
        {
            var insert = Sql.InsertInto(ProductsTable)
                .Columns(Products.Name, Products.Price)
                .Values("Widget1", 10.00m)
                .Values("Widget2", 20.00m)
                .Values("Widget3", 30.00m);
            var result = insert.Build();
            AssertContainsDml(result.Sql, "INSERT INTO", "VALUES");
            Assert(result.Parameters.Count == 6, "Should have 6 parameters for 3 rows × 2 columns");
        });

        RunTest("Insert_BulkValues_GeneratesBulkInsert", () =>
        {
            var rows = new List<object?[]>
            {
                new object?[] { "Product1", 10.00m },
                new object?[] { "Product2", 20.00m },
                new object?[] { "Product3", 30.00m }
            };
            var insert = Sql.InsertInto(ProductsTable)
                .Columns(Products.Name, Products.Price)
                .BulkValues(rows);
            var result = insert.Build();
            AssertContainsDml(result.Sql, "INSERT INTO", "VALUES");
            Assert(result.Parameters.Count == 6, "Should have 6 parameters");
        });

        RunTest("Insert_WithNull_GeneratesNullValue", () =>
        {
            var insert = Sql.InsertInto(ProductsTable)
                .Columns(Products.Name, Products.Price, Products.NullableCategoryId)
                .Values("Widget", 99.99m, null);
            var result = insert.Build();
            AssertContainsDml(result.Sql, "NULL");
            Assert(result.Parameters.Count == 2, "Should have 2 non-null parameters");
        });

        RunTest("Insert_FromSelect_GeneratesInsertSelect", () =>
        {
            var selectQuery = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Name, Products.Price, Products.CategoryId);
            var insert = Sql.InsertInto(new Table("ProductsArchive", "dbo"))
                .Columns(Products.Name, Products.Price, Products.CategoryId)
                .Select(selectQuery);
            var result = insert.Build();
            AssertContainsDml(result.Sql, "INSERT INTO", "SELECT", "FROM [dbo].[Products]", "WHERE");
        });

        RunTest("Insert_WithOutput_GeneratesOutputClause", () =>
        {
            var insert = Sql.InsertInto(ProductsTable)
                .Columns(Products.Name, Products.Price)
                .Values("Widget", 99.99m)
                .Output(Products.Id);
            var result = insert.Build();
            AssertContainsDml(result.Sql, "OUTPUT INSERTED.[Id]");
        });

        RunTest("Insert_DefaultValues_GeneratesDefaultValuesSyntax", () =>
        {
            var insert = Sql.InsertInto(ProductsTable);
            // Access internal builder via interface chain (columns then build)
            var builder = (InsertBuilder)insert;
            var result = builder.Build();
            AssertContainsDml(result.Sql, "DEFAULT VALUES");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 16: UPDATE Statement Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── UPDATE Statement Tests ──");

        RunTest("Update_SingleColumn_GeneratesValidSql", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.Price, 199.99m)
                .Where(Products.Id == 1);
            var result = update.Build();
            AssertContainsDml(result.Sql, "UPDATE [dbo].[Products]", "SET [Price] = @p0", "WHERE");
            Assert(result.Parameters.ContainsKey("@p0"), "Should have parameter @p0");
        });

        RunTest("Update_MultipleColumns_GeneratesMultipleSets", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.Name, "Updated Widget")
                .Set(Products.Price, 299.99m)
                .Set(Products.Status, "Active")
                .Where(Products.Id == 1);
            var result = update.Build();
            AssertContainsDml(result.Sql, "SET [Name] = @p0", "[Price] = @p1", "[Status] = @p2");
        });

        RunTest("Update_SetToNull_GeneratesNullAssignment", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.NullableCategoryId, (int?)null)
                .Where(Products.Id == 1);
            var result = update.Build();
            AssertContainsDml(result.Sql, "SET [CategoryId] = NULL");
        });

        RunTest("Update_SetToColumn_GeneratesColumnReference", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.Price, Products.Price)  // Set price to itself (for demo)
                .Where(Products.Id == 1);
            var result = update.Build();
            AssertContainsDml(result.Sql, "SET [Price] = [Products].[Price]");
        });

        RunTest("Update_WithComplexWhere_GeneratesWhereClause", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.Status, "Archived")
                .Where(Products.Price < 10)
                .And(Products.Status == "Active");
            var result = update.Build();
            AssertContainsDml(result.Sql, "WHERE", "AND");
        });

        RunTest("Update_WithOutput_GeneratesOutputClause", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.Status, "Updated")
                .Where(Products.Id == 1)
                .Output(Products.Id, Products.Name);
            var result = update.Build();
            AssertContainsDml(result.Sql, "OUTPUT INSERTED.[Id], INSERTED.[Name]");
        });

        RunTest("Update_CorrelatedWithFrom_GeneratesFromClause", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.Status, "Popular")
                .From(OrderItemsTable)
                .Where((Products.Id == OrderItems.ProductId) & (OrderItems.Quantity > 100));
            var result = update.Build();
            AssertContainsDml(result.Sql, "UPDATE [dbo].[Products]", "FROM [dbo].[OrderItems]", "WHERE");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 17: DELETE Statement Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── DELETE Statement Tests ──");

        RunTest("Delete_WithWhere_GeneratesValidSql", () =>
        {
            var delete = Sql.DeleteFrom(ProductsTable)
                .Where(Products.Status == "Deleted");
            var result = delete.Build();
            AssertContainsDml(result.Sql, "DELETE FROM [dbo].[Products]", "WHERE [Products].[Status] = @p0");
        });

        RunTest("Delete_WithComplexWhere_GeneratesComplexCondition", () =>
        {
            var delete = Sql.DeleteFrom(ProductsTable)
                .Where(Products.Price < 1)
                .And(Products.Status == "Inactive")
                .Or(Products.Status == "Archived");
            var result = delete.Build();
            AssertContainsDml(result.Sql, "DELETE FROM", "WHERE", "AND", "OR");
        });

        RunTest("Delete_WithOutput_GeneratesOutputClause", () =>
        {
            var delete = Sql.DeleteFrom(ProductsTable)
                .Where(Products.Status == "ToDelete")
                .Output(Products.Id, Products.Name);
            var result = delete.Build();
            AssertContainsDml(result.Sql, "OUTPUT DELETED.[Id], DELETED.[Name]");
        });

        RunTest("Delete_WhereIn_GeneratesInSubquery", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .GroupBy(OrderItems.ProductId)
                .Having(Fn.Count() == 0)
                .Select(OrderItems.ProductId);
            var delete = Sql.DeleteFrom(ProductsTable)
                .WhereIn(Products.Id, subquery);
            var result = delete.Build();
            AssertContainsDml(result.Sql, "DELETE FROM", "WHERE [Products].[Id] IN (SELECT");
        });

        RunTest("Delete_WhereExists_GeneratesExistsSubquery", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId == Products.Id)
                .Select(Sql.Literal(1));
            var delete = Sql.DeleteFrom(ProductsTable)
                .WhereExists(subquery);
            var result = delete.Build();
            AssertContainsDml(result.Sql, "DELETE FROM", "WHERE EXISTS (SELECT");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 18: MERGE Statement Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── MERGE Statement Tests ──");

        RunTest("Merge_UsingTable_GeneratesValidSql", () =>
        {
            var merge = Sql.MergeInto(ProductsTable)
                .Using(new Table("ProductUpdates", "dbo"))
                .On(Products.Id == Sql.Col<int>("ProductUpdates.Id"))
                .WhenMatched().ThenUpdate()
                    .Set(Products.Price, Sql.Col<decimal>("ProductUpdates.Price"))
                    .Build();
            var result = merge.Build();
            AssertContainsDml(result.Sql, "MERGE INTO [dbo].[Products] AS TARGET", "USING [dbo].[ProductUpdates] AS SOURCE", "ON", "WHEN MATCHED", "THEN UPDATE SET");
            AssertContainsDml(result.Sql, ";"); // MERGE must end with semicolon
        });

        RunTest("Merge_WhenMatchedDelete_GeneratesDeleteAction", () =>
        {
            var merge = Sql.MergeInto(ProductsTable)
                .Using(new Table("ProductsToDelete", "dbo"))
                .On(Products.Id == Sql.Col<int>("ProductsToDelete.Id"))
                .WhenMatched().ThenDelete()
                .Build();
            var result = merge.Build();
            AssertContainsDml(result.Sql, "WHEN MATCHED THEN DELETE");
        });

        RunTest("Merge_WhenNotMatchedInsert_GeneratesInsertAction", () =>
        {
            var merge = Sql.MergeInto(ProductsTable)
                .Using(new Table("NewProducts", "dbo"))
                .On(Products.Id == Sql.Col<int>("NewProducts.Id"))
                .WhenNotMatchedByTarget().ThenInsert(Products.Id, Products.Name, Products.Price)
                    .ValuesFromSource()
                .Build();
            var result = merge.Build();
            AssertContainsDml(result.Sql, "WHEN NOT MATCHED BY TARGET THEN INSERT", "VALUES (SOURCE.[Id], SOURCE.[Name], SOURCE.[Price])");
        });

        RunTest("Merge_WhenMatchedAnd_GeneratesConditionalUpdate", () =>
        {
            var merge = Sql.MergeInto(ProductsTable)
                .Using(new Table("ProductUpdates", "dbo"))
                .On(Products.Id == Sql.Col<int>("ProductUpdates.Id"))
                .WhenMatchedAnd(Products.Status == "Active").ThenUpdate()
                    .Set(Products.Price, Sql.Col<decimal>("ProductUpdates.Price"))
                    .Build();
            var result = merge.Build();
            AssertContainsDml(result.Sql, "WHEN MATCHED AND", "THEN UPDATE SET");
        });

        RunTest("Merge_MultipleWhenClauses_GeneratesAllClauses", () =>
        {
            var merge = Sql.MergeInto(ProductsTable)
                .Using(new Table("ProductUpdates", "dbo"))
                .On(Products.Id == Sql.Col<int>("ProductUpdates.Id"))
                .WhenMatched().ThenUpdate()
                    .Set(Products.Price, Sql.Col<decimal>("ProductUpdates.Price"))
                    .And()
                .WhenNotMatchedByTarget().ThenInsert(Products.Id, Products.Name, Products.Price)
                    .ValuesFromSource()
                    .And()
                .WhenNotMatchedBySource().ThenDelete()
                .Build();
            var result = merge.Build();
            AssertContainsDml(result.Sql, "WHEN MATCHED", "WHEN NOT MATCHED BY TARGET", "WHEN NOT MATCHED BY SOURCE");
        });

        RunTest("Merge_WithOutput_GeneratesOutputClause", () =>
        {
            var merge = Sql.MergeInto(ProductsTable)
                .Using(new Table("ProductUpdates", "dbo"))
                .On(Products.Id == Sql.Col<int>("ProductUpdates.Id"))
                .WhenMatched().ThenUpdate()
                    .Set(Products.Price, Sql.Col<decimal>("ProductUpdates.Price"))
                    .Build()
                .Output(Products.Id);
            var result = merge.Build();
            AssertContainsDml(result.Sql, "OUTPUT INSERTED.[Id]");
        });

        RunTest("Merge_UsingSubquery_GeneratesSubquerySource", () =>
        {
            var sourceQuery = Sql.From(new Table("TempProducts", "dbo"))
                .Where(Sql.Col<decimal>("TempProducts.Price") > 0)
                .Select(Sql.Col<int>("TempProducts.Id"), Sql.Col<string>("TempProducts.Name"), Sql.Col<decimal>("TempProducts.Price"));
            var merge = Sql.MergeInto(ProductsTable)
                .Using(sourceQuery, "src")
                .On(Products.Id == Sql.Col<int>("src.Id"))
                .WhenMatched().ThenUpdate()
                    .Set(Products.Price, Sql.Col<decimal>("src.Price"))
                    .Build();
            var result = merge.Build();
            AssertContainsDml(result.Sql, "USING (SELECT", ") AS [src]");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 19: DML Parameterization Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── DML Parameterization Tests ──");

        RunTest("Insert_SqlInjection_Parameterized", () =>
        {
            var malicious = "'; DELETE FROM Products; --";
            var insert = Sql.InsertInto(ProductsTable)
                .Columns(Products.Name, Products.Price)
                .Values(malicious, 99.99m);
            var result = insert.Build();
            AssertNotContainsDml(result.Sql, "DELETE");
            Assert(result.Parameters["@p0"]?.ToString() == malicious, "Malicious string should be in parameter");
        });

        RunTest("Update_SqlInjection_Parameterized", () =>
        {
            var malicious = "'; DROP TABLE Products; --";
            var update = Sql.Update(ProductsTable)
                .Set(Products.Name, malicious)
                .Where(Products.Id == 1);
            var result = update.Build();
            AssertNotContainsDml(result.Sql, "DROP TABLE");
        });

        RunTest("Delete_SqlInjection_Parameterized", () =>
        {
            var malicious = "1 OR 1=1; --";
            var delete = Sql.DeleteFrom(ProductsTable)
                .Where(Products.Name == malicious);
            var result = delete.Build();
            AssertNotContainsDml(result.Sql, "1=1");
            Assert(result.Parameters.ContainsKey("@p0"), "Should use parameter");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 20: Parameter Consistency Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Parameter Consistency Tests ──");

        RunTest("InsertSelect_ParametersMatch", () =>
        {
            var selectQuery = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .And(Products.Name == "Test")
                .Select(Products.Name, Products.Price, Products.CategoryId);
            var insert = Sql.InsertInto(new Table("Archive", "dbo"))
                .Columns(Products.Name, Products.Price, Products.CategoryId)
                .Select(selectQuery);
            var result = insert.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("DeleteWhereIn_ParametersMatch", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 500m)
                .Select(Products.Id);
            var delete = Sql.DeleteFrom(ProductsTable)
                .WhereIn(Products.Id, subquery);
            var result = delete.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("DeleteWhereExists_ParametersMatch", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 500m)
                .And(Products.Status == "Active")
                .Select(Sql.Literal(1));
            var delete = Sql.DeleteFrom(ProductsTable)
                .WhereExists(subquery);
            var result = delete.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("MergeUsingSubquery_ParametersMatch", () =>
        {
            var sourceQuery = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .Select(Products.Id, Products.Name, Products.Price);
            var merge = Sql.MergeInto(new Table("TargetProducts", "dbo"))
                .Using(sourceQuery, "Source")
                .On(Sql.Col<int>("TargetProducts.Id") == Sql.Col<int>("Source.Id"))
                .WhenMatched()
                .ThenUpdate()
                .Set(Products.Price, Sql.Col<decimal>("Source.Price"))
                .Build();
            var result = merge.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("SelectInSubquery_ParametersMatch", () =>
        {
            var subquery = Sql.From(OrdersTable)
                .Where(Orders.Total > 1000m)
                .Select(Orders.CustomerId);
            var query = Sql.From(CustomersTable)
                .Where(Customers.Id.In(subquery))
                .Select(Customers.Name);
            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("SelectExistsSubquery_ParametersMatch", () =>
        {
            var subquery = Sql.From(OrdersTable)
                .Where(Orders.CustomerId == Customers.Id)
                .And(Orders.Total > 500m)
                .Select(Sql.Literal(1));
            var query = Sql.From(CustomersTable)
                .Where(Sql.Exists(subquery))
                .Select(Customers.Name);
            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("CteWithParameters_ParametersMatch", () =>
        {
            var query = Sql.With("Expensive", () =>
                    Sql.From(ProductsTable)
                        .Where(Products.Price > 500m)
                        .And(Products.Status == "Active")
                        .Select(Products.Id, Products.Name, Products.Price))
                .From(Sql.Cte("Expensive"))
                .SelectAll();
            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("UnionWithParameters_ParametersMatch", () =>
        {
            var q1 = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .Select(Products.Id, Products.Name);
            var q2 = Sql.From(ProductsTable)
                .Where(Products.Status == "Featured")
                .Select(Products.Id, Products.Name);
            var query = q1.Union(q2).Select(Sql.All);
            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 21: Advanced Subquery Edge Cases
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Advanced Subquery Edge Cases ──");

        RunTest("MultipleInSubqueries_ParametersNotSwapped", () =>
        {
            // Two IN subqueries with different parameter values
            var subquery1 = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)  // @p0 = 100
                .Select(Products.Id);
            var subquery2 = Sql.From(ProductsTable)
                .Where(Products.Price < 50m)   // @p1 = 50
                .Select(Products.CategoryId);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(subquery1))
                .And(OrderItems.OrderId.In(subquery2))
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);

            // Verify the values didn't get swapped
            var values = result.Parameters.Values.Cast<object>().OrderBy(x => Convert.ToDecimal(x)).ToList();
            Assert(values.Count == 2, $"Expected 2 parameters, got {values.Count}");
            Assert(Convert.ToDecimal(values[0]) == 50m, $"Expected 50, got {values[0]}");
            Assert(Convert.ToDecimal(values[1]) == 100m, $"Expected 100, got {values[1]}");
        });

        RunTest("ThreeNestedSubqueries_ParametersPreserved", () =>
        {
            // Three levels of nesting
            var innermost = Sql.From(OrderItemsTable)
                .Where(OrderItems.Quantity > 10)  // param 1
                .Select(OrderItems.ProductId);
            var middle = Sql.From(ProductsTable)
                .Where(Products.Id.In(innermost))
                .And(Products.Price > 200m)       // param 2
                .Select(Products.CategoryId);
            var query = Sql.From(CategoriesTable)
                .Where(Categories.Id.In(middle))
                .And(Categories.Name == "Electronics")  // param 3
                .Select(Categories.Name);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        RunTest("SubqueryWithMultipleConditions_AllParametersIncluded", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .And(Products.Price < 500m)
                .And(Products.Status == "Active")
                .And(Products.Name.Like("%Widget%"))
                .Select(Products.Id);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(subquery))
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 4, $"Expected 4 parameters, got {result.Parameters.Count}");
        });

        RunTest("ExistsWithComplexSubquery_AllParametersIncluded", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId == Products.Id)
                .And(OrderItems.Quantity > 5)
                .And(OrderItems.Amount > 100m)
                .Select(Sql.Literal(1));

            var query = Sql.From(ProductsTable)
                .Where(Sql.Exists(subquery))
                .And(Products.Status == "Active")
                .Select(Products.Name);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        RunTest("NotExistsWithParameters_AllParametersIncluded", () =>
        {
            var subquery = Sql.From(OrdersTable)
                .Where(Orders.CustomerId == Customers.Id)
                .And(Orders.Total > 1000m)
                .Select(Sql.Literal(1));

            var query = Sql.From(CustomersTable)
                .Where(Sql.NotExists(subquery))
                .Select(Customers.Name);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 1, $"Expected 1 parameter, got {result.Parameters.Count}");
        });

        RunTest("InAndNotInSubqueries_BothParametersIncluded", () =>
        {
            var subquery1 = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .Select(Products.Id);
            var subquery2 = Sql.From(ProductsTable)
                .Where(Products.Status == "Discontinued")
                .Select(Products.Id);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(subquery1))
                .And(OrderItems.ProductId.NotIn(subquery2))
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("OrConditionWithSubqueries_ParametersPreserved", () =>
        {
            var subquery1 = Sql.From(ProductsTable)
                .Where(Products.Price > 1000m)
                .Select(Products.Id);
            var subquery2 = Sql.From(ProductsTable)
                .Where(Products.Status == "Featured")
                .Select(Products.Id);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(subquery1) | OrderItems.ProductId.In(subquery2))
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("JoinWithSubqueryParameters_ParametersPreserved", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 500m)
                .And(Products.Status == "Premium")
                .Select(Products.Id, Products.Name, Products.CategoryId);

            var query = Sql.From(CategoriesTable)
                .Join(subquery, "PremiumProducts").On(Categories.Id == Sql.Col<int>("PremiumProducts.CategoryId"))
                .Select(Categories.Name, Sql.Col<string>("PremiumProducts.Name"));

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("LeftJoinWithSubqueryParameters_ParametersPreserved", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.Quantity > 10)
                .GroupBy(OrderItems.ProductId)
                .Select(OrderItems.ProductId, Fn.Sum(OrderItems.Amount).As("TotalAmount"));

            var query = Sql.From(ProductsTable)
                .LeftJoin(subquery, "Sales").On(Products.Id == Sql.Col<int>("Sales.ProductId"))
                .Where(Products.Price > 100m)
                .Select(Products.Name, Sql.Col<decimal?>("Sales.TotalAmount"));

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("MultipleCtes_AllParametersPreserved", () =>
        {
            var query = Sql
                .With("Expensive", () => Sql.From(ProductsTable)
                    .Where(Products.Price > 500m)
                    .Select(Products.Id, Products.Name, Products.CategoryId))
                .With("ActiveCustomers", () => Sql.From(CustomersTable)
                    .Where(Customers.Status == "Active")
                    .Select(Customers.Id, Customers.Name))
                .From(Sql.Cte("Expensive"))
                .Join(Sql.Cte("ActiveCustomers")).On(Sql.Col<int>("Expensive.Id") == Sql.Col<int>("ActiveCustomers.Id"))
                .SelectAll();

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("CteReferencedMultipleTimes_ParametersCorrect", () =>
        {
            var query = Sql
                .With("PremiumProducts", () => Sql.From(ProductsTable)
                    .Where(Products.Price > 1000m)
                    .Select(Products.Id, Products.CategoryId))
                .From(Sql.Cte("PremiumProducts"))
                .SelectAll();

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 1, $"Expected 1 parameter, got {result.Parameters.Count}");
        });

        RunTest("UnionAllWithDifferentParameters_AllPreserved", () =>
        {
            var q1 = Sql.From(ProductsTable)
                .Where(Products.Price > 500m)
                .Select(Products.Id, Products.Name);
            var q2 = Sql.From(ProductsTable)
                .Where(Products.Price < 10m)
                .Select(Products.Id, Products.Name);
            var q3 = Sql.From(ProductsTable)
                .Where(Products.Status == "Featured")
                .Select(Products.Id, Products.Name);

            // Chain two unions (UnionAll returns IOrderBy which can Select then result is IQuery)
            var combined = q1.UnionAll(q2).Select(Sql.All);
            var query = combined.UnionAll(q3).Select(Sql.All);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        RunTest("SubqueryFromClause_WithWhereParameters_AllPreserved", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .And(Products.Status == "Active")
                .Select(Products.Id, Products.Name, Products.CategoryId);

            var query = Sql.From(subquery, "FilteredProducts")
                .Where(Sql.Col<int>("FilteredProducts.CategoryId") == 5)
                .Select(Sql.Col<string>("FilteredProducts.Name"));

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 22: DML Advanced Edge Cases
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── DML Advanced Edge Cases ──");

        RunTest("InsertSelectWithMultipleConditions_AllParametersPreserved", () =>
        {
            var selectQuery = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .And(Products.Price < 1000m)
                .And(Products.Status == "Active")
                .And(Products.CategoryId == 5)
                .Select(Products.Name, Products.Price, Products.CategoryId);

            var insert = Sql.InsertInto(new Table("Archive", "dbo"))
                .Columns(Products.Name, Products.Price, Products.CategoryId)
                .Select(selectQuery);

            var result = insert.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 4, $"Expected 4 parameters, got {result.Parameters.Count}");
        });

        RunTest("DeleteWhereInWithComplexSubquery_AllParametersPreserved", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.Quantity < 5)
                .And(OrderItems.Amount < 10m)
                .GroupBy(OrderItems.ProductId)
                .Having(Fn.Count() > 3)
                .Select(OrderItems.ProductId);

            var delete = Sql.DeleteFrom(ProductsTable)
                .WhereIn(Products.Id, subquery);

            var result = delete.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        RunTest("UpdateWithWhereInSubquery_ParametersPreserved", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.Quantity > 100)
                .Select(OrderItems.ProductId);

            var update = Sql.Update(ProductsTable)
                .Set(Products.Status, "Popular")
                .Where(Products.Id.In(subquery))
                .And(Products.Price > 50m);

            var result = update.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        RunTest("MergeWithComplexSourceQuery_AllParametersPreserved", () =>
        {
            var sourceQuery = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .And(Products.Status == "Active")
                .Select(Products.Id, Products.Name, Products.Price);

            var merge = Sql.MergeInto(new Table("TargetProducts", "dbo"))
                .Using(sourceQuery, "src")
                .On(Sql.Col<int>("TargetProducts.Id") == Sql.Col<int>("src.Id"))
                .WhenMatched()
                .ThenUpdate()
                .Set(Products.Price, Sql.Col<decimal>("src.Price"))
                .Build();

            var result = merge.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("UpdateFromWithSubqueryJoin_ParametersPreserved", () =>
        {
            var update = Sql.Update(ProductsTable)
                .Set(Products.Status, "TopSeller")
                .From(OrderItemsTable)
                .Where((Products.Id == OrderItems.ProductId) & (OrderItems.Quantity > 1000));

            var result = update.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 23: Parameter Value Integrity Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Parameter Value Integrity Tests ──");

        RunTest("ParameterValues_NotSwappedInUnion", () =>
        {
            var q1 = Sql.From(ProductsTable)
                .Where(Products.Price == 111.11m)
                .Select(Products.Id);
            var q2 = Sql.From(ProductsTable)
                .Where(Products.Price == 222.22m)
                .Select(Products.Id);

            var query = q1.Union(q2).Select(Sql.All);
            var result = query.Build();

            // Get values sorted by parameter name to check ordering
            var sortedParams = result.Parameters.OrderBy(p => p.Key).ToList();
            var v0 = Convert.ToDecimal(sortedParams[0].Value);
            var v1 = Convert.ToDecimal(sortedParams[1].Value);

            // The first value in the SQL should be first in parameters
            var firstInSql = result.Sql.IndexOf("@p0") < result.Sql.IndexOf("@p1");
            Assert(firstInSql, "Parameters should appear in order");
            Assert(v0 == 111.11m || v1 == 111.11m, "111.11 should be present");
            Assert(v0 == 222.22m || v1 == 222.22m, "222.22 should be present");
        });

        RunTest("ParameterValues_NotSwappedInNestedSubqueries", () =>
        {
            var inner = Sql.From(ProductsTable)
                .Where(Products.Price == 50m)
                .Select(Products.Id);
            var outer = Sql.From(OrdersTable)
                .Where(Orders.CustomerId.In(inner))
                .And(Orders.Status == "Active")
                .Select(Orders.Total);

            var result = outer.Build();

            // Verify 50m and "Active" are both present and not swapped
            var decimalParam = result.Parameters.Values.FirstOrDefault(v => v is decimal);
            var stringParam = result.Parameters.Values.FirstOrDefault(v => v is string);

            Assert(decimalParam != null && Convert.ToDecimal(decimalParam) == 50m, "Decimal value 50 should be present");
            Assert(stringParam != null && stringParam.ToString() == "Active", "String value 'Active' should be present");
        });

        RunTest("ParameterValues_PreservedInCte", () =>
        {
            var query = Sql.With("Test", () =>
                    Sql.From(ProductsTable)
                        .Where(Products.Price == 999.99m)
                        .And(Products.Name == "TestProduct")
                        .Select(Products.Id))
                .From(Sql.Cte("Test"))
                .SelectAll();

            var result = query.Build();

            var decimalParam = result.Parameters.Values.FirstOrDefault(v => v is decimal);
            var stringParam = result.Parameters.Values.FirstOrDefault(v => v is string);

            Assert(decimalParam != null && Convert.ToDecimal(decimalParam) == 999.99m, "Decimal 999.99 should be present");
            Assert(stringParam != null && stringParam.ToString() == "TestProduct", "String 'TestProduct' should be present");
        });

        RunTest("ParameterValues_ManyParametersOrdered", () =>
        {
            // Create a query with 10+ parameters to ensure ordering is correct
            var query = Sql.From(ProductsTable)
                .Where(Products.Price > 1m)
                .And(Products.Price < 10m)
                .And(Products.Status == "A")
                .And(Products.Name == "B")
                .And(Products.CategoryId == 100)
                .And(Products.CategoryId != 200)
                .Select(Products.Id);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);

            // Check all values are present
            var values = result.Parameters.Values.ToList();
            Assert(values.Contains(1m), "Should contain 1m");
            Assert(values.Contains(10m), "Should contain 10m");
            Assert(values.Contains("A"), "Should contain 'A'");
            Assert(values.Contains("B"), "Should contain 'B'");
            Assert(values.Contains(100), "Should contain 100");
            Assert(values.Contains(200), "Should contain 200");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 24: Complex Condition Chains
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Complex Condition Chains ──");

        RunTest("ConditionChain_AndOrWithSubqueries", () =>
        {
            var subquery1 = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .Select(Products.Id);
            var subquery2 = Sql.From(ProductsTable)
                .Where(Products.Status == "Premium")
                .Select(Products.Id);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(subquery1) & (OrderItems.Quantity > 5) | OrderItems.ProductId.In(subquery2))
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        RunTest("NotConditionWithSubquery_ParametersPreserved", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.Quantity > 10)
                .Select(OrderItems.ProductId);

            var query = Sql.From(ProductsTable)
                .Where(!Products.Id.In(subquery))
                .And(Products.Price > 50m)
                .Select(Products.Name);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("MultipleOrConditionsWithSubqueries_AllParametersPreserved", () =>
        {
            var sub1 = Sql.From(ProductsTable).Where(Products.Price > 100m).Select(Products.Id);
            var sub2 = Sql.From(ProductsTable).Where(Products.Price > 200m).Select(Products.Id);
            var sub3 = Sql.From(ProductsTable).Where(Products.Price > 300m).Select(Products.Id);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(sub1) | OrderItems.ProductId.In(sub2) | OrderItems.ProductId.In(sub3))
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 3, $"Expected 3 parameters, got {result.Parameters.Count}");
        });

        RunTest("NestedConditionGroups_ParametersPreserved", () =>
        {
            // ((A AND B) OR (C AND D)) AND E
            var condA = Products.Price > 100m;
            var condB = Products.Price < 200m;
            var condC = Products.Status == "Active";
            var condD = Products.Name.Like("%Widget%");
            var condE = Products.CategoryId == 5;

            var query = Sql.From(ProductsTable)
                .Where(((condA & condB) | (condC & condD)) & condE)
                .Select(Products.Id);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 5, $"Expected 5 parameters, got {result.Parameters.Count}");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 25: Edge Cases for HAVING and GROUP BY
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── HAVING and GROUP BY Edge Cases ──");

        RunTest("HavingWithMultipleAggregates_ParametersPreserved", () =>
        {
            // Test HAVING with multiple aggregate conditions
            var query = Sql.From(ProductsTable)
                .GroupBy(Products.CategoryId)
                .Having((Fn.Avg(Products.Price) > 100m) & (Fn.Max(Products.Price) < 1000m))
                .Select(Products.CategoryId, Fn.Avg(Products.Price).As("AvgPrice"));

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("GroupByHavingWithMultipleConditions_AllParametersPreserved", () =>
        {
            var query = Sql.From(OrderItemsTable)
                .GroupBy(OrderItems.ProductId)
                .Having((Fn.Sum(OrderItems.Amount) > 1000m) & (Fn.Count() > 5))
                .Select(OrderItems.ProductId, Fn.Sum(OrderItems.Amount).As("Total"));

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 26: Scalar Subquery Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Scalar Subquery Tests ──");

        RunTest("ScalarSubquery_EqualityComparison", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Status == "Reference")
                .Select(Fn.Max(Products.Price));

            var query = Sql.From(ProductsTable)
                .Where(Products.Price == Sql.Scalar<decimal>(subquery))
                .Select(Products.Name);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        RunTest("ScalarSubquery_GreaterThanComparison", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.CategoryId == 5)
                .Select(Fn.Avg(Products.Price));

            var query = Sql.From(ProductsTable)
                .Where(Products.Price > Sql.Scalar<decimal>(subquery))
                .And(Products.CategoryId != 5)
                .Select(Products.Name);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 2, $"Expected 2 parameters, got {result.Parameters.Count}");
        });

        RunTest("ScalarSubquery_InSelect", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId == Products.Id)
                .Select(Fn.Sum(OrderItems.Amount));

            var query = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .Select(Products.Name, Sql.Scalar<decimal>(subquery).As("TotalSales"));

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 27: Stress Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Stress Tests ──");

        RunTest("ManyParameters_20Plus_AllPreserved", () =>
        {
            // Build a query with 20+ parameters
            var query = Sql.From(ProductsTable)
                .Where(Products.Price > 1m)
                .And(Products.Price < 2m)
                .And(Products.Price > 3m)
                .And(Products.Price < 4m)
                .And(Products.Price > 5m)
                .And(Products.Price < 6m)
                .And(Products.Price > 7m)
                .And(Products.Price < 8m)
                .And(Products.Price > 9m)
                .And(Products.Price < 10m)
                .And(Products.Status == "s1")
                .And(Products.Status == "s2")
                .And(Products.Status == "s3")
                .And(Products.Status == "s4")
                .And(Products.Status == "s5")
                .And(Products.Name == "n1")
                .And(Products.Name == "n2")
                .And(Products.Name == "n3")
                .And(Products.Name == "n4")
                .And(Products.Name == "n5")
                .Select(Products.Id);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 20, $"Expected 20 parameters, got {result.Parameters.Count}");
        });

        RunTest("DeepSubqueryNesting_5Levels", () =>
        {
            var level5 = Sql.From(ProductsTable).Where(Products.Price > 5m).Select(Products.Id);
            var level4 = Sql.From(ProductsTable).Where(Products.Id.In(level5)).And(Products.Price > 4m).Select(Products.Id);
            var level3 = Sql.From(ProductsTable).Where(Products.Id.In(level4)).And(Products.Price > 3m).Select(Products.Id);
            var level2 = Sql.From(ProductsTable).Where(Products.Id.In(level3)).And(Products.Price > 2m).Select(Products.Id);
            var level1 = Sql.From(ProductsTable).Where(Products.Id.In(level2)).And(Products.Price > 1m).Select(Products.Name);

            var result = level1.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 5, $"Expected 5 parameters, got {result.Parameters.Count}");
        });

        RunTest("ComplexUnionChain_AllParametersPreserved", () =>
        {
            var q1 = Sql.From(ProductsTable).Where(Products.Price > 100m).Select(Products.Id, Products.Name);
            var q2 = Sql.From(ProductsTable).Where(Products.Price > 200m).Select(Products.Id, Products.Name);
            var q3 = Sql.From(ProductsTable).Where(Products.Price > 300m).Select(Products.Id, Products.Name);
            var q4 = Sql.From(ProductsTable).Where(Products.Price > 400m).Select(Products.Id, Products.Name);
            var q5 = Sql.From(ProductsTable).Where(Products.Price > 500m).Select(Products.Id, Products.Name);

            // Chain unions (Union returns IOrderBy, need Select to get IQuery)
            var u1 = q1.Union(q2).Select(Sql.All);
            var u2 = u1.Union(q3).Select(Sql.All);
            var u3 = u2.Union(q4).Select(Sql.All);
            var query = u3.Union(q5).Select(Sql.All);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);
            Assert(result.Parameters.Count == 5, $"Expected 5 parameters, got {result.Parameters.Count}");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 28: Specific Bug Regression Tests
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine("\n── Bug Regression Tests ──");

        RunTest("ParameterRemapping_NoCollisions", () =>
        {
            // This tests the bug where @p1 was being replaced before @p10 causing issues
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 1m)
                .And(Products.Price > 2m)
                .And(Products.Price > 3m)
                .And(Products.Price > 4m)
                .And(Products.Price > 5m)
                .And(Products.Price > 6m)
                .And(Products.Price > 7m)
                .And(Products.Price > 8m)
                .And(Products.Price > 9m)
                .And(Products.Price > 10m)
                .And(Products.Price > 11m)
                .Select(Products.Id);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(subquery))
                .And(OrderItems.Amount > 100m)
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);

            // Verify all values are present and distinct (except possibly 11m and 100m which are different)
            var decimals = result.Parameters.Values.Where(v => v is decimal).Select(v => (decimal)v!).ToList();
            Assert(decimals.Count == 12, $"Expected 12 decimal parameters, got {decimals.Count}");
        });

        RunTest("BarePlaceholder_NotReplacedInsideExisting", () =>
        {
            // Test that @p isn't matched inside @p0, @p1, etc.
            var query = Sql.From(ProductsTable)
                .Where(Products.Name == "test1")
                .And(Products.Name == "test2")
                .And(Products.Name == "test3")
                .Select(Products.Id);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);

            // Ensure no bare @p remains
            Assert(!result.Sql.Contains("@p "), "Should not contain '@p ' (bare placeholder)");
            Assert(!result.Sql.Contains("@p)"), "Should not contain '@p)' (bare placeholder)");
        });

        RunTest("SubqueryParameterRemapping_CorrectOrder", () =>
        {
            // Subquery has @p0, @p1, main query adds more
            // Ensure after remapping, all parameters are sequential
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 100m)
                .And(Products.Status == "Active")
                .Select(Products.Id);

            var query = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId.In(subquery))
                .And(OrderItems.Quantity > 10)
                .And(OrderItems.Amount > 50m)
                .Select(OrderItems.Amount);

            var result = query.Build();
            AssertParametersMatchSql(result.Sql, result.Parameters);

            // Verify parameters are @p0 through @p3
            Assert(result.Parameters.ContainsKey("@p0"), "Should have @p0");
            Assert(result.Parameters.ContainsKey("@p1"), "Should have @p1");
            Assert(result.Parameters.ContainsKey("@p2"), "Should have @p2");
            Assert(result.Parameters.ContainsKey("@p3"), "Should have @p3");
            Assert(result.Parameters.Count == 4, $"Expected exactly 4 parameters, got {result.Parameters.Count}");
        });

        // ═══════════════════════════════════════════════════════════════════════════
        // Print Summary
        // ═══════════════════════════════════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"Results: {_passed} passed, {_failed} failed, {_passed + _failed} total");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");

        Environment.Exit(_failed > 0 ? 1 : 0);
    }

    static void RunTest(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"  [PASS] {name}");
            _passed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] {name}");
            Console.WriteLine($"         {ex.Message}");
            if (_verbose && ex.StackTrace != null)
            {
                var lines = ex.StackTrace.Split('\n').Take(3);
                foreach (var line in lines)
                    Console.WriteLine($"         {line.Trim()}");
            }
            _failed++;
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void AssertParametersMatchSql(string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        // Extract all parameter references from SQL
        var sqlParams = System.Text.RegularExpressions.Regex.Matches(sql, @"@p\d+")
            .Select(m => m.Value)
            .Distinct()
            .ToHashSet();

        // Check all SQL params exist in dictionary
        foreach (var param in sqlParams)
        {
            if (!parameters.ContainsKey(param))
            {
                throw new Exception($"Parameter '{param}' in SQL not found in parameters dictionary.\n\nSQL:\n{sql}\n\nParameters:\n{string.Join(", ", parameters.Keys)}");
            }
        }

        // Check all dictionary params exist in SQL
        foreach (var key in parameters.Keys)
        {
            if (!sqlParams.Contains(key))
            {
                throw new Exception($"Parameter '{key}' in dictionary not found in SQL.\n\nSQL:\n{sql}\n\nParameters:\n{string.Join(", ", parameters.Keys)}");
            }
        }
    }

    static void AssertContainsDml(string sql, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (!sql.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"DML does not contain expected pattern: '{pattern}'\n\nSQL:\n{sql}");
            }
        }
    }

    static void AssertNotContainsDml(string sql, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (sql.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"DML contains unexpected pattern: '{pattern}'\n\nSQL:\n{sql}");
            }
        }
    }
}
