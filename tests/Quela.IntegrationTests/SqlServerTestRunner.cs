using Microsoft.Data.SqlClient;

namespace Quela.IntegrationTests;

/// <summary>
/// Runs integration tests against a real SQL Server instance.
/// </summary>
public class SqlServerTestRunner : IDisposable
{
    private readonly SqlConnection _connection;
    private readonly string _testDbName;
    private bool _disposed;

    /// <summary>
    /// Creates a test runner with the given connection string.
    /// </summary>
    public SqlServerTestRunner(string connectionString)
    {
        _testDbName = $"QuelaTest_{Guid.NewGuid():N}".Substring(0, 30);
        _connection = new SqlConnection(connectionString);
    }

    /// <summary>
    /// Initializes the test database with schema.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        // Create test database
        await ExecuteNonQueryAsync($"CREATE DATABASE [{_testDbName}]");
        await ExecuteNonQueryAsync($"USE [{_testDbName}]");

        // Create test schema
        await ExecuteNonQueryAsync(@"
            CREATE TABLE [dbo].[Categories] (
                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                [Name] NVARCHAR(100) NOT NULL
            );

            CREATE TABLE [dbo].[Products] (
                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                [Name] NVARCHAR(200) NOT NULL,
                [Price] DECIMAL(18,2) NOT NULL,
                [CategoryId] INT NULL REFERENCES [dbo].[Categories]([Id]),
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active'
            );

            CREATE TABLE [dbo].[Customers] (
                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                [Name] NVARCHAR(200) NOT NULL,
                [Email] NVARCHAR(200) NOT NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active'
            );

            CREATE TABLE [dbo].[Orders] (
                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                [CustomerId] INT NOT NULL REFERENCES [dbo].[Customers]([Id]),
                [OrderDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [Total] DECIMAL(18,2) NOT NULL DEFAULT 0,
                [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending'
            );

            CREATE TABLE [dbo].[OrderItems] (
                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                [OrderId] INT NOT NULL REFERENCES [dbo].[Orders]([Id]),
                [ProductId] INT NOT NULL REFERENCES [dbo].[Products]([Id]),
                [Quantity] INT NOT NULL DEFAULT 1,
                [Amount] DECIMAL(18,2) NOT NULL
            );

            CREATE TABLE [dbo].[Employees] (
                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                [Name] NVARCHAR(200) NOT NULL,
                [ManagerId] INT NULL REFERENCES [dbo].[Employees]([Id]),
                [Salary] DECIMAL(18,2) NOT NULL,
                [Department] NVARCHAR(100) NOT NULL
            );
        ");

        // Seed test data
        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // Categories
        await ExecuteNonQueryAsync(@"
            INSERT INTO [dbo].[Categories] ([Name]) VALUES
            ('Electronics'), ('Clothing'), ('Books'), ('Home'), ('Sports');
        ");

        // Products
        await ExecuteNonQueryAsync(@"
            INSERT INTO [dbo].[Products] ([Name], [Price], [CategoryId], [Status]) VALUES
            ('Laptop', 1299.99, 1, 'Active'),
            ('Smartphone', 899.99, 1, 'Active'),
            ('T-Shirt', 29.99, 2, 'Active'),
            ('Jeans', 79.99, 2, 'Active'),
            ('Novel', 14.99, 3, 'Active'),
            ('Textbook', 89.99, 3, 'Active'),
            ('Lamp', 49.99, 4, 'Active'),
            ('Chair', 199.99, 4, 'Featured'),
            ('Basketball', 29.99, 5, 'Active'),
            ('Tennis Racket', 149.99, 5, 'Featured'),
            ('Discontinued Item', 9.99, NULL, 'Deleted');
        ");

        // Customers
        await ExecuteNonQueryAsync(@"
            INSERT INTO [dbo].[Customers] ([Name], [Email], [Status]) VALUES
            ('John Doe', 'john@example.com', 'Active'),
            ('Jane Smith', 'jane@example.com', 'Active'),
            ('Bob Wilson', 'bob@example.com', 'Inactive');
        ");

        // Orders
        await ExecuteNonQueryAsync(@"
            INSERT INTO [dbo].[Orders] ([CustomerId], [Total], [Status]) VALUES
            (1, 1329.98, 'Completed'),
            (1, 109.98, 'Completed'),
            (2, 899.99, 'Pending'),
            (2, 279.98, 'Completed');
        ");

        // Order Items
        await ExecuteNonQueryAsync(@"
            INSERT INTO [dbo].[OrderItems] ([OrderId], [ProductId], [Quantity], [Amount]) VALUES
            (1, 1, 1, 1299.99),
            (1, 3, 1, 29.99),
            (2, 5, 2, 29.98),
            (2, 4, 1, 79.99),
            (3, 2, 1, 899.99),
            (4, 8, 1, 199.99),
            (4, 4, 1, 79.99);
        ");

        // Employees (hierarchical)
        await ExecuteNonQueryAsync(@"
            INSERT INTO [dbo].[Employees] ([Name], [ManagerId], [Salary], [Department]) VALUES
            ('CEO', NULL, 250000, 'Executive'),
            ('VP Sales', 1, 150000, 'Sales'),
            ('VP Engineering', 1, 160000, 'Engineering'),
            ('Sales Manager', 2, 90000, 'Sales'),
            ('Engineer 1', 3, 85000, 'Engineering'),
            ('Engineer 2', 3, 80000, 'Engineering'),
            ('Sales Rep 1', 4, 55000, 'Sales'),
            ('Sales Rep 2', 4, 52000, 'Sales');
        ");
    }

    /// <summary>
    /// Executes a query built with Quela and returns the results.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(IQuery query)
    {
        var built = query.Build();
        return await ExecuteQueryAsync(built.Sql, built.Parameters);
    }

    /// <summary>
    /// Executes raw SQL with parameters and returns results.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        await using var cmd = new SqlCommand(sql, _connection);

        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var results = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[name] = value;
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Executes SQL and returns scalar result.
    /// </summary>
    public async Task<T?> ExecuteScalarAsync<T>(IQuery query)
    {
        var built = query.Build();
        await using var cmd = new SqlCommand(built.Sql, _connection);

        foreach (var (name, value) in built.Parameters)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var result = await cmd.ExecuteScalarAsync();
        if (result is null or DBNull)
            return default;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Executes non-query SQL.
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(string sql)
    {
        await using var cmd = new SqlCommand(sql, _connection);
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Verifies that a query executes without error.
    /// </summary>
    public async Task AssertExecutesAsync(IQuery query)
    {
        try
        {
            await ExecuteQueryAsync(query);
        }
        catch (SqlException ex)
        {
            throw new Exception($"Query failed to execute:\n{query.ToSql()}\n\nError: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies query returns expected row count.
    /// </summary>
    public async Task AssertRowCountAsync(IQuery query, int expectedCount)
    {
        var results = await ExecuteQueryAsync(query);
        if (results.Count != expectedCount)
        {
            throw new Exception($"Expected {expectedCount} rows, got {results.Count}\n\nSQL:\n{query.ToSql()}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            // Switch to master and drop test database
            using var masterConn = new SqlConnection(_connection.ConnectionString.Replace(_testDbName, "master"));
            masterConn.Open();
            using var cmd = new SqlCommand($@"
                ALTER DATABASE [{_testDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_testDbName}];
            ", masterConn);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            _connection.Dispose();
        }
    }
}
