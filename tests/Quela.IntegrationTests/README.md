# Quela Integration Tests

This project contains integration tests that validate Quela's SQL generation against:

1. **ScriptDom Parser** - Microsoft's official SQL Server T-SQL parser for syntax validation
2. **SQL Server Database** - Execute queries against a real SQL Server instance

## Prerequisites

```bash
# Enable NuGet package restore (remove <clear /> from nuget.config)
# Then restore packages:
dotnet restore
```

### Required NuGet Packages

- `Microsoft.SqlServer.TransactSql.ScriptDom` (161.*) - T-SQL syntax validation
- `Microsoft.Data.SqlClient` (5.1.*) - SQL Server connectivity

## Running ScriptDom Tests (No SQL Server Required)

The ScriptDom tests validate SQL syntax without needing a running SQL Server:

```bash
dotnet run --project tests/Quela.IntegrationTests
```

## Running SQL Server Integration Tests

### Option 1: Docker

```bash
# Start SQL Server container
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2022-latest

# Run tests
QUELA_TEST_CONNECTION="Server=localhost;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true" \
  dotnet run --project tests/Quela.IntegrationTests
```

### Option 2: Existing SQL Server

```bash
# Set connection string
export QUELA_TEST_CONNECTION="Server=your-server;Database=master;User Id=sa;Password=yourpassword;TrustServerCertificate=true"

# Or pass as argument
dotnet run --project tests/Quela.IntegrationTests -- --connection="Server=...;..."
```

### Option 3: LocalDB (Windows)

```bash
QUELA_TEST_CONNECTION="Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true" \
  dotnet run --project tests/Quela.IntegrationTests
```

## Test Coverage

### ScriptDom Tests (~20 tests)
- Basic SELECT statements
- WHERE with multiple conditions
- All JOIN types (INNER, LEFT, RIGHT, FULL, CROSS)
- Aggregate functions (COUNT, SUM, AVG, MIN, MAX)
- Window functions (ROW_NUMBER, RANK, DENSE_RANK, LAG, LEAD)
- Common Table Expressions (CTEs)
- Set operations (UNION, UNION ALL, INTERSECT, EXCEPT)
- Subqueries (IN, EXISTS, derived tables)
- CASE expressions
- Complex multi-table queries

### SQL Server Integration Tests (~25 tests)
- Query execution and result verification
- Row count validation
- Aggregate results
- Window function output verification
- JOIN result validation
- Pagination (OFFSET/FETCH)
- CTE execution
- Complex query execution

## Test Output

```
Quela SQL DSL - Integration Tests
══════════════════════════════════════════════════════════════════════

── ScriptDom SQL Server Parser Validation ──
  [PASS] ScriptDom_SimpleSelect
  [PASS] ScriptDom_SelectWithWhere
  ...

── SQL Server Integration Tests ──
  Connected to SQL Server, running integration tests...

  [PASS] SqlServer_SimpleSelect
  [PASS] SqlServer_SelectWithWhere
  ...

══════════════════════════════════════════════════════════════════════
Results: 45 passed, 0 failed, 0 skipped
══════════════════════════════════════════════════════════════════════
```

## Verbose Mode

For detailed error output:

```bash
dotnet run --project tests/Quela.IntegrationTests -- --verbose
```
