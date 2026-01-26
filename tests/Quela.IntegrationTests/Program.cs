using Quela;
using Quela.IntegrationTests;
using static Quela.IntegrationTests.TestSchema;

namespace Quela.IntegrationTests;

/// <summary>
/// Integration tests for Quela SQL DSL.
/// - ScriptDom tests: Validate SQL syntax using SQL Server's official parser
/// - SQL Server tests: Execute queries against a real SQL Server instance
/// </summary>
class Program
{
    static int _passed = 0;
    static int _failed = 0;
    static int _skipped = 0;
    static bool _verbose = false;
    static SqlServerTestRunner? _sqlServer;

    static async Task<int> Main(string[] args)
    {
        _verbose = args.Contains("--verbose") || args.Contains("-v");
        var connectionString = Environment.GetEnvironmentVariable("QUELA_TEST_CONNECTION")
            ?? GetConnectionStringFromArgs(args);

        Console.WriteLine("Quela SQL DSL - Integration Tests");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Run ScriptDom validation tests (always available)
        Console.WriteLine("── ScriptDom SQL Server Parser Validation ──");
        await RunScriptDomTests();

        // Run SQL Server integration tests (if connection available)
        if (!string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("\n── SQL Server Integration Tests ──");
            try
            {
                _sqlServer = new SqlServerTestRunner(connectionString);
                await _sqlServer.InitializeAsync();
                Console.WriteLine("  Connected to SQL Server, running integration tests...\n");
                await RunSqlServerTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [SKIP] Could not connect to SQL Server: {ex.Message}");
                _skipped++;
            }
            finally
            {
                _sqlServer?.Dispose();
            }
        }
        else
        {
            Console.WriteLine("\n── SQL Server Integration Tests ──");
            Console.WriteLine("  [SKIP] No connection string provided.");
            Console.WriteLine("         Set QUELA_TEST_CONNECTION environment variable or pass --connection=<string>");
            _skipped++;
        }

        // Summary
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"Results: {_passed} passed, {_failed} failed, {_skipped} skipped");
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");

        return _failed > 0 ? 1 : 0;
    }

    static string? GetConnectionStringFromArgs(string[] args)
    {
        var connArg = args.FirstOrDefault(a => a.StartsWith("--connection="));
        return connArg?.Substring("--connection=".Length);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ScriptDom Tests - Validate SQL syntax against SQL Server's official parser
    // ═══════════════════════════════════════════════════════════════════════════

    static async Task RunScriptDomTests()
    {
        await Task.CompletedTask; // Make async for consistency

        // Basic SELECT
        RunTest("ScriptDom_SimpleSelect", () =>
        {
            var sql = Sql.From(ProductsTable).Select(Products.Id, Products.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_SelectWithWhere", () =>
        {
            var sql = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_SelectWithMultipleConditions", () =>
        {
            var sql = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .And(Products.Status == "Active")
                .Or(Products.Status == "Featured")
                .Select(Products.Id).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // JOINs
        RunTest("ScriptDom_InnerJoin", () =>
        {
            var sql = Sql.From(ProductsTable)
                .Join(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_MultipleJoins", () =>
        {
            var sql = Sql.From(OrdersTable)
                .Join(CustomersTable).On(Orders.CustomerId == Customers.Id)
                .Join(OrderItemsTable).On(OrderItems.OrderId == Orders.Id)
                .Join(ProductsTable).On(OrderItems.ProductId == Products.Id)
                .Select(Orders.Id, Customers.Name, Products.Name, OrderItems.Amount).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_LeftJoin", () =>
        {
            var sql = Sql.From(ProductsTable)
                .LeftJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // Aggregates
        RunTest("ScriptDom_GroupByWithHaving", () =>
        {
            var sql = Sql.From(ProductsTable)
                .GroupBy(Products.CategoryId)
                .Having(Fn.Count() > 5)
                .Select(Products.CategoryId, Fn.Count(), Fn.Avg(Products.Price)).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_AllAggregates", () =>
        {
            var sql = Sql.From(ProductsTable)
                .Select(
                    Fn.Count(),
                    Fn.Sum(Products.Price),
                    Fn.Avg(Products.Price),
                    Fn.Min(Products.Price),
                    Fn.Max(Products.Price),
                    Fn.CountDistinct(Products.CategoryId)).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // Window Functions
        RunTest("ScriptDom_RowNumber", () =>
        {
            var sql = Sql.From(ProductsTable)
                .Select(Products.Id,
                    Window.RowNumber()
                        .Over(o => o.PartitionBy(Products.CategoryId).OrderBy(Products.Price.Desc()))
                        .As("Rank")).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_AllWindowFunctions", () =>
        {
            var sql = Sql.From(ProductsTable)
                .Select(
                    Products.Id,
                    Window.RowNumber().Over(o => o.OrderBy(Products.Id.Asc())).As("RowNum"),
                    Window.Rank().Over(o => o.OrderBy(Products.Price.Desc())).As("Rank"),
                    Window.DenseRank().Over(o => o.OrderBy(Products.Price.Desc())).As("DenseRank"),
                    Window.Lag(Products.Price, 1).Over(o => o.OrderBy(Products.Id.Asc())).As("PrevPrice"),
                    Window.Lead(Products.Price, 1).Over(o => o.OrderBy(Products.Id.Asc())).As("NextPrice")).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // CTEs
        RunTest("ScriptDom_SimpleCTE", () =>
        {
            var sql = Sql.With("ExpensiveProducts", () =>
                    Sql.From(ProductsTable)
                        .Where(Products.Price > 1000)
                        .Select(Products.Id, Products.Name, Products.CategoryId))
                .From(Sql.Cte("ExpensiveProducts"))
                .Join(CategoriesTable).On(Sql.Col<int>("ExpensiveProducts.CategoryId") == Categories.Id)
                .Select(Sql.Col<string>("ExpensiveProducts.Name"), Categories.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_MultipleCTEs", () =>
        {
            var sql = Sql
                .With("Expensive", () => Sql.From(ProductsTable).Where(Products.Price > 500).Select(Products.Id, Products.CategoryId))
                .With("Popular", () => Sql.From(OrderItemsTable).GroupBy(OrderItems.ProductId).Having(Fn.Count() > 2).Select(OrderItems.ProductId))
                .From(Sql.Cte("Expensive"))
                .SelectAll().ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // Set Operations
        RunTest("ScriptDom_Union", () =>
        {
            var q1 = Sql.From(ProductsTable).Where(Products.Price > 100).Select(Products.Id, Products.Name);
            var q2 = Sql.From(ProductsTable).Where(Products.Status == "Featured").Select(Products.Id, Products.Name);
            var sql = q1.Union(q2).Select(Sql.All).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_UnionAll", () =>
        {
            var q1 = Sql.From(ProductsTable).Where(Products.Price > 100).Select(Products.Id, Products.Name);
            var q2 = Sql.From(ProductsTable).Where(Products.Status == "Featured").Select(Products.Id, Products.Name);
            var sql = q1.UnionAll(q2).Select(Sql.All).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // Subqueries
        RunTest("ScriptDom_InSubquery", () =>
        {
            var subquery = Sql.From(OrderItemsTable).Select(OrderItems.ProductId);
            var sql = Sql.From(ProductsTable)
                .Where(Products.Id.In(subquery))
                .Select(Products.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_ExistsSubquery", () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId == Products.Id)
                .Select(Sql.Literal(1));
            var sql = Sql.From(ProductsTable)
                .Where(Sql.Exists(subquery))
                .Select(Products.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        RunTest("ScriptDom_DerivedTable", () =>
        {
            var subquery = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name, Products.CategoryId);
            var sql = Sql.From(subquery, "Expensive")
                .Join(CategoriesTable).On(Sql.Col<int>("Expensive.CategoryId") == Categories.Id)
                .Select(Sql.Col<string>("Expensive.Name"), Categories.Name).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // CASE Expression
        RunTest("ScriptDom_CaseExpression", () =>
        {
            var caseExpr = Case.When(Products.Price > 1000, "Expensive")
                               .When(Products.Price > 100, "Medium")
                               .Else("Cheap");
            var sql = Sql.From(ProductsTable)
                .Select(Products.Name, caseExpr.As("PriceCategory")).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // Complex Query
        RunTest("ScriptDom_ComplexQuery", () =>
        {
            var sql = Sql.From(OrdersTable)
                .Join(CustomersTable).On(Orders.CustomerId == Customers.Id)
                .Join(OrderItemsTable).On(OrderItems.OrderId == Orders.Id)
                .Join(ProductsTable).On(OrderItems.ProductId == Products.Id)
                .Join(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Where(Customers.Status == "Active")
                .And(Orders.Status != "Cancelled")
                .GroupBy(Customers.Name, Categories.Name)
                .Having(Fn.Sum(OrderItems.Amount) > 100)
                .OrderBy(Fn.Sum(OrderItems.Amount).Desc())
                .Select(
                    Customers.Name,
                    Categories.Name,
                    Fn.Count().As("OrderCount"),
                    Fn.Sum(OrderItems.Amount).As("TotalSpent"))
                .OffsetFetch(0, 10).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // Pagination
        RunTest("ScriptDom_Pagination", () =>
        {
            var sql = Sql.From(ProductsTable)
                .OrderBy(Products.Price.Desc())
                .Select(Products.Id, Products.Name, Products.Price)
                .OffsetFetch(20, 10).ToSql();
            ScriptDomValidator.AssertValid(sql);
        });

        // Special characters in parameters (SQL injection test)
        RunTest("ScriptDom_ParameterizedQuery", () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Name == "O'Brien")
                .And(Products.Status.In("Active", "Pending"))
                .Select(Products.Id);
            var built = query.Build();
            ScriptDomValidator.AssertValid(built.Sql);
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SQL Server Integration Tests - Execute against real database
    // ═══════════════════════════════════════════════════════════════════════════

    static async Task RunSqlServerTests()
    {
        if (_sqlServer == null) return;

        // Basic Queries
        await RunTestAsync("SqlServer_SimpleSelect", async () =>
        {
            var query = Sql.From(ProductsTable).Select(Products.Id, Products.Name, Products.Price);
            await _sqlServer.AssertRowCountAsync(query, 11);
        });

        await RunTestAsync("SqlServer_SelectWithWhere", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name);
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 6, $"Expected 6 products > $100, got {results.Count}");
        });

        await RunTestAsync("SqlServer_WhereWithNull", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.CategoryId.IsNull())
                .Select(Products.Id, Products.Name);
            await _sqlServer.AssertRowCountAsync(query, 1);
        });

        await RunTestAsync("SqlServer_WhereIn", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.Status.In("Active", "Featured"))
                .Select(Products.Id);
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 10, $"Expected 10 active/featured products, got {results.Count}");
        });

        // JOINs
        await RunTestAsync("SqlServer_InnerJoin", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Join(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 10, $"Expected 10 products with categories, got {results.Count}");
        });

        await RunTestAsync("SqlServer_LeftJoin", async () =>
        {
            var query = Sql.From(ProductsTable)
                .LeftJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
                .Select(Products.Name, Categories.Name);
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 11, $"Expected 11 products (including NULL category), got {results.Count}");
        });

        await RunTestAsync("SqlServer_MultiTableJoin", async () =>
        {
            var query = Sql.From(OrdersTable)
                .Join(CustomersTable).On(Orders.CustomerId == Customers.Id)
                .Join(OrderItemsTable).On(OrderItems.OrderId == Orders.Id)
                .Join(ProductsTable).On(OrderItems.ProductId == Products.Id)
                .Select(Customers.Name, Products.Name, OrderItems.Quantity, OrderItems.Amount);
            await _sqlServer.AssertExecutesAsync(query);
        });

        // Aggregates
        await RunTestAsync("SqlServer_Count", async () =>
        {
            var query = Sql.From(ProductsTable).Select(Fn.Count());
            var count = await _sqlServer.ExecuteScalarAsync<int>(query);
            Assert(count == 11, $"Expected 11 products, got {count}");
        });

        await RunTestAsync("SqlServer_GroupByCount", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.CategoryId.IsNotNull())
                .GroupBy(Products.CategoryId)
                .Select(Products.CategoryId, Fn.Count().As("ProductCount"));
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 5, $"Expected 5 categories, got {results.Count}");
        });

        await RunTestAsync("SqlServer_Having", async () =>
        {
            var query = Sql.From(ProductsTable)
                .GroupBy(Products.CategoryId)
                .Having(Fn.Count() >= 2)
                .Select(Products.CategoryId, Fn.Count());
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.All(r => (int)r[""]! >= 2), "All groups should have count >= 2");
        });

        await RunTestAsync("SqlServer_SumAvgMinMax", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(
                    Fn.Sum(Products.Price).As("Total"),
                    Fn.Avg(Products.Price).As("Average"),
                    Fn.Min(Products.Price).As("Min"),
                    Fn.Max(Products.Price).As("Max"));
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 1, "Should return one row of aggregates");
        });

        // Window Functions
        await RunTestAsync("SqlServer_RowNumber", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Select(
                    Products.Id,
                    Products.Name,
                    Window.RowNumber()
                        .Over(o => o.OrderBy(Products.Price.Desc()))
                        .As("PriceRank"));
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 11, "Should return all products with rank");
            // Verify ranking is correct
            var firstRank = (long)results.First()["PriceRank"]!;
            Assert(firstRank == 1, "First row should have rank 1");
        });

        await RunTestAsync("SqlServer_PartitionedRowNumber", async () =>
        {
            var query = Sql.From(ProductsTable)
                .Where(Products.CategoryId.IsNotNull())
                .Select(
                    Products.CategoryId,
                    Products.Name,
                    Window.RowNumber()
                        .Over(o => o.PartitionBy(Products.CategoryId).OrderBy(Products.Price.Desc()))
                        .As("CategoryRank"));
            var results = await _sqlServer.ExecuteQueryAsync(query);
            // Each category should have items starting at rank 1
            var firstPerCategory = results.GroupBy(r => r["CategoryId"]).Select(g => g.First());
            Assert(firstPerCategory.All(r => (long)r["CategoryRank"]! == 1), "Each category should start at rank 1");
        });

        // Subqueries
        await RunTestAsync("SqlServer_InSubquery", async () =>
        {
            var orderedProducts = Sql.From(OrderItemsTable).Select(OrderItems.ProductId);
            var query = Sql.From(ProductsTable)
                .Where(Products.Id.In(orderedProducts))
                .Select(Products.Name);
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count > 0, "Should find ordered products");
        });

        await RunTestAsync("SqlServer_ExistsSubquery", async () =>
        {
            var subquery = Sql.From(OrderItemsTable)
                .Where(OrderItems.ProductId == Products.Id)
                .Select(Sql.Literal(1));
            var query = Sql.From(ProductsTable)
                .Where(Sql.Exists(subquery))
                .Select(Products.Name);
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count > 0, "Should find products with orders");
        });

        // CTEs
        await RunTestAsync("SqlServer_CTE", async () =>
        {
            var query = Sql.With("ExpensiveProducts", () =>
                    Sql.From(ProductsTable)
                        .Where(Products.Price > 500)
                        .Select(Products.Id, Products.Name, Products.Price))
                .From(Sql.Cte("ExpensiveProducts"))
                .SelectAll();
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.All(r => (decimal)r["Price"]! > 500), "All products should be > $500");
        });

        // Pagination
        await RunTestAsync("SqlServer_Pagination", async () =>
        {
            var query = Sql.From(ProductsTable)
                .OrderBy(Products.Price.Desc())
                .Select(Products.Id, Products.Name, Products.Price)
                .OffsetFetch(2, 3);
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.Count == 3, $"Expected 3 rows with FETCH 3, got {results.Count}");
        });

        // Set Operations
        await RunTestAsync("SqlServer_Union", async () =>
        {
            var expensive = Sql.From(ProductsTable).Where(Products.Price > 500).Select(Products.Id, Products.Name);
            var featured = Sql.From(ProductsTable).Where(Products.Status == "Featured").Select(Products.Id, Products.Name);
            var query = expensive.Union(featured).Select(Sql.All);
            await _sqlServer.AssertExecutesAsync(query);
        });

        // CASE Expression
        await RunTestAsync("SqlServer_Case", async () =>
        {
            var caseExpr = Case.When(Products.Price > 500, "Premium")
                               .When(Products.Price > 50, "Standard")
                               .Else("Budget");
            var query = Sql.From(ProductsTable)
                .Select(Products.Name, Products.Price, caseExpr.As("Tier"));
            var results = await _sqlServer.ExecuteQueryAsync(query);
            Assert(results.All(r => r.ContainsKey("Tier")), "All rows should have Tier column");
        });

        // Complex Query
        await RunTestAsync("SqlServer_ComplexQuery", async () =>
        {
            var query = Sql.From(OrdersTable)
                .Join(CustomersTable).On(Orders.CustomerId == Customers.Id)
                .Join(OrderItemsTable).On(OrderItems.OrderId == Orders.Id)
                .Join(ProductsTable).On(OrderItems.ProductId == Products.Id)
                .GroupBy(Customers.Name)
                .Having(Fn.Sum(OrderItems.Amount) > 50)
                .OrderBy(Fn.Sum(OrderItems.Amount).Desc())
                .Select(Customers.Name, Fn.Sum(OrderItems.Amount).As("TotalSpent"), Fn.Count().As("ItemCount"))
                .OffsetFetch(0, 10);
            await _sqlServer.AssertExecutesAsync(query);
        });
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
                foreach (var line in ex.StackTrace.Split('\n').Take(3))
                    Console.WriteLine($"         {line.Trim()}");
            }
            _failed++;
        }
    }

    static async Task RunTestAsync(string name, Func<Task> test)
    {
        try
        {
            await test();
            Console.WriteLine($"  [PASS] {name}");
            _passed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] {name}");
            Console.WriteLine($"         {ex.Message}");
            if (_verbose && ex.StackTrace != null)
            {
                foreach (var line in ex.StackTrace.Split('\n').Take(3))
                    Console.WriteLine($"         {line.Trim()}");
            }
            _failed++;
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
