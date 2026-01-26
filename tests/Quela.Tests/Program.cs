using Quela;
using static Quela.Tests.Db;

namespace Quela.Tests;

/// <summary>
/// Simple test runner that validates the SQL DSL works correctly.
/// </summary>
class Program
{
    static int _passed = 0;
    static int _failed = 0;

    static void Main(string[] args)
    {
        Console.WriteLine("Quela SQL DSL - Test Runner");
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Basic Query Tests
        RunTest("SimpleSelect_GeneratesValidSql", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Select(Products.Id, Products.Name, Products.Price);

            var sql = query.ToSql();
            Assert(sql.Contains("SELECT"), "Should contain SELECT");
            Assert(sql.Contains("[Products].[Id]"), "Should contain Products.Id");
            Assert(sql.Contains("[Products].[Name]"), "Should contain Products.Name");
            Assert(sql.Contains("[Products].[Price]"), "Should contain Products.Price");
            Assert(sql.Contains("FROM [dbo].[Products]"), "Should contain FROM clause");
        });

        RunTest("SelectAll_GeneratesStarSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .SelectAll();

            var sql = query.ToSql();
            Assert(sql.Contains("SELECT *"), "Should contain SELECT *");
        });

        RunTest("SelectWithAlias_GeneratesAsSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Select(Products.Name.As("ProductName"), Products.Price.As("UnitPrice"));

            var sql = query.ToSql();
            Assert(sql.Contains("[Products].[Name] AS [ProductName]"), "Should contain Name AS ProductName");
            Assert(sql.Contains("[Products].[Price] AS [UnitPrice]"), "Should contain Price AS UnitPrice");
        });

        RunTest("SelectDistinct_GeneratesDistinctKeyword", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .SelectDistinct(Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("SELECT DISTINCT"), "Should contain SELECT DISTINCT");
        });

        RunTest("SelectTop_GeneratesTopClause", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .SelectTop(10, Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("SELECT TOP (10)"), "Should contain TOP (10)");
        });

        RunTest("WhereWithEquals_ParameterizesValue", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.Name == "Widget")
                .Select(Products.Id);

            var result = query.Build();
            Assert(result.Sql.Contains("WHERE [Products].[Name] = @p0"), "Should parameterize value");
            Assert(result.Parameters.ContainsKey("@p0"), "Should have parameter @p0");
            Assert(result.Parameters["@p0"]?.ToString() == "Widget", "Parameter should be Widget");
        });

        RunTest("WhereWithNull_GeneratesIsNullSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.NullableCategoryId.IsNull())
                .Select(Products.Id);

            var sql = query.ToSql();
            Assert(sql.Contains("[Products].[CategoryId] IS NULL"), "Should contain IS NULL");
        });

        RunTest("MultipleWhereClauses_CombinedWithAnd", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.Price > 100)
                .And(Products.Status == "Active")
                .Select(Products.Id);

            var result = query.Build();
            Assert(result.Sql.Contains("WHERE"), "Should contain WHERE");
            Assert(result.Sql.Contains("[Products].[Price] > @p0"), "Should contain Price > @p0");
            Assert(result.Sql.Contains("AND"), "Should contain AND");
        });

        RunTest("OrderByAsc_GeneratesAscKeyword", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .OrderBy(Products.Name.Asc())
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("ORDER BY [Products].[Name] ASC"), "Should contain ORDER BY ASC");
        });

        RunTest("OrderByDesc_GeneratesDescKeyword", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .OrderBy(Products.Price.Desc())
                .Select(Products.Id, Products.Price);

            var sql = query.ToSql();
            Assert(sql.Contains("ORDER BY [Products].[Price] DESC"), "Should contain ORDER BY DESC");
        });

        RunTest("OffsetFetch_GeneratesPaginationClause", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .OrderBy(Products.Id.Asc())
                .Select(Products.Id, Products.Name)
                .OffsetFetch(10, 20);

            var sql = query.ToSql();
            Assert(sql.Contains("OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY"), "Should contain OFFSET/FETCH");
        });

        // Join Tests
        RunTest("InnerJoin_GeneratesJoinSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Join(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("INNER JOIN [dbo].[Categories]"), "Should contain INNER JOIN");
            Assert(sql.Contains("ON [Products].[CategoryId] = [Categories].[Id]"), "Should contain ON clause");
        });

        RunTest("LeftJoin_GeneratesLeftJoinSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .LeftJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("LEFT OUTER JOIN [dbo].[Categories]"), "Should contain LEFT OUTER JOIN");
        });

        RunTest("RightJoin_GeneratesRightJoinSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .RightJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("RIGHT OUTER JOIN [dbo].[Categories]"), "Should contain RIGHT OUTER JOIN");
        });

        RunTest("MultipleJoins_GeneratesMultipleJoinClauses", () =>
        {
            var query = Sql
                .From(OrdersTable)
                .Join(CustomersTable).On(Orders.CustomerId == Customers.Id)
                .Join(OrderItemsTable).On(OrderItems.OrderId == Orders.Id)
                .Select(Orders.Id, Customers.Name, OrderItems.Amount);

            var sql = query.ToSql();
            Assert(sql.Contains("INNER JOIN [dbo].[Customers]"), "Should contain first JOIN");
            Assert(sql.Contains("INNER JOIN [dbo].[OrderItems]"), "Should contain second JOIN");
        });

        // Aggregate Tests
        RunTest("Count_GeneratesCountSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Select(Fn.Count());

            var sql = query.ToSql();
            Assert(sql.Contains("COUNT(*)"), "Should contain COUNT(*)");
        });

        RunTest("CountColumn_GeneratesCountColumnSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Select(Fn.Count(Products.CategoryId));

            var sql = query.ToSql();
            Assert(sql.Contains("COUNT([Products].[CategoryId])"), "Should contain COUNT(column)");
        });

        RunTest("Sum_GeneratesSumSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Select(Fn.Sum(Products.Price));

            var sql = query.ToSql();
            Assert(sql.Contains("SUM([Products].[Price])"), "Should contain SUM(column)");
        });

        RunTest("GroupBy_GeneratesGroupBySyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .GroupBy(Products.CategoryId)
                .Select(Products.CategoryId, Fn.Count());

            var sql = query.ToSql();
            Assert(sql.Contains("GROUP BY [Products].[CategoryId]"), "Should contain GROUP BY");
        });

        RunTest("Having_GeneratesHavingSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .GroupBy(Products.CategoryId)
                .Having(Fn.Count() > 5)
                .Select(Products.CategoryId, Fn.Count());

            var sql = query.ToSql();
            Assert(sql.Contains("HAVING COUNT(*) >"), "Should contain HAVING");
        });

        // Window Function Tests
        RunTest("RowNumber_GeneratesRowNumberSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Select(
                    Products.Id,
                    Window.RowNumber()
                          .Over(o => o.OrderBy(Products.Price.Desc()))
                          .As("RowNum"));

            var sql = query.ToSql();
            Assert(sql.Contains("ROW_NUMBER()"), "Should contain ROW_NUMBER()");
            Assert(sql.Contains("OVER"), "Should contain OVER");
            Assert(sql.Contains("ORDER BY"), "Should contain ORDER BY in OVER");
        });

        RunTest("RowNumberWithPartition_GeneratesPartitionBySyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Select(
                    Products.Id,
                    Window.RowNumber()
                          .Over(o => o.PartitionBy(Products.CategoryId)
                                      .OrderBy(Products.Price.Desc()))
                          .As("RowNum"));

            var sql = query.ToSql();
            Assert(sql.Contains("PARTITION BY [Products].[CategoryId]"), "Should contain PARTITION BY");
        });

        // CTE Tests
        RunTest("SimpleCte_GeneratesWithClause", () =>
        {
            var query = Sql
                .With("ExpensiveProducts", () => Sql
                    .From(ProductsTable)
                    .Where(Products.Price > 1000)
                    .Select(Products.Id, Products.Name, Products.Price))
                .From(Sql.Cte("ExpensiveProducts"))
                .SelectAll();

            var sql = query.ToSql();
            Assert(sql.Contains("WITH [ExpensiveProducts] AS"), "Should contain WITH clause");
            Assert(sql.Contains("FROM [ExpensiveProducts]"), "Should contain FROM CTE");
        });

        // Set Operations Tests
        RunTest("Union_GeneratesUnionSyntax", () =>
        {
            var query1 = Sql
                .From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name);

            var query2 = Sql
                .From(ProductsTable)
                .Where(Products.Status == "Featured")
                .Select(Products.Id, Products.Name);

            var combined = query1.Union(query2).Select(Sql.All);
            var sql = combined.ToSql();
            Assert(sql.Contains("UNION"), "Should contain UNION");
        });

        RunTest("UnionAll_GeneratesUnionAllSyntax", () =>
        {
            var query1 = Sql
                .From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name);

            var query2 = Sql
                .From(ProductsTable)
                .Where(Products.Status == "Featured")
                .Select(Products.Id, Products.Name);

            var combined = query1.UnionAll(query2).Select(Sql.All);
            var sql = combined.ToSql();
            Assert(sql.Contains("UNION ALL"), "Should contain UNION ALL");
        });

        // Subquery Tests
        RunTest("InSubquery_GeneratesInSubquerySyntax", () =>
        {
            var subquery = Sql
                .From(OrderItemsTable)
                .Select(OrderItems.ProductId);

            var query = Sql
                .From(ProductsTable)
                .Where(Products.Id.In(subquery))
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("IN (SELECT"), "Should contain IN (SELECT...)");
        });

        RunTest("ExistsSubquery_GeneratesExistsSyntax", () =>
        {
            var subquery = Sql
                .From(OrderItemsTable)
                .Where(OrderItems.ProductId == Products.Id)
                .Select(Sql.Literal(1));

            var query = Sql
                .From(ProductsTable)
                .Where(Sql.Exists(subquery))
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("EXISTS (SELECT"), "Should contain EXISTS (SELECT...)");
        });

        // Between Tests
        RunTest("Between_GeneratesBetweenSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.Price.Between(10m, 100m))
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("BETWEEN"), "Should contain BETWEEN");
        });

        // Like Tests
        RunTest("Like_GeneratesLikeSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.Name.Like("Widget%"))
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("LIKE"), "Should contain LIKE");
        });

        // IsNull / IsNotNull Tests
        RunTest("IsNull_GeneratesIsNullSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.CategoryId.IsNull())
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("IS NULL"), "Should contain IS NULL");
        });

        RunTest("IsNotNull_GeneratesIsNotNullSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.CategoryId.IsNotNull())
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("IS NOT NULL"), "Should contain IS NOT NULL");
        });

        // In list Tests
        RunTest("InList_GeneratesInSyntax", () =>
        {
            var query = Sql
                .From(ProductsTable)
                .Where(Products.Status.In("Active", "Pending", "Review"))
                .Select(Products.Id, Products.Name);

            var sql = query.ToSql();
            Assert(sql.Contains("IN ("), "Should contain IN (...)");
        });

        // Print summary
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════");
        Console.WriteLine($"Results: {_passed} passed, {_failed} failed");
        Console.WriteLine("══════════════════════════════════════════════════════════════");

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
            _failed++;
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
