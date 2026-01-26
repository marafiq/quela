using FluentAssertions;
using Quela.Tests.Parsing;
using Xunit;
using static Quela.Tests.Db;

namespace Quela.Tests.Features;

public class WindowFunctionTests
{
    [Fact]
    public void RowNumber_WithOrderBy_GeneratesCorrectSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(
                Products.Id,
                Products.Name,
                Window.RowNumber()
                    .Over(o => o.OrderBy(Products.Price.Desc()))
                    .As("Rank"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("ROW_NUMBER() OVER (ORDER BY [Products].[Price] DESC)");
    }

    [Fact]
    public void RowNumber_WithPartitionBy_GeneratesPartitionClause()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(
                Products.Id,
                Products.Name,
                Window.RowNumber()
                    .Over(o => o
                        .PartitionBy(Products.CategoryId)
                        .OrderBy(Products.Price.Desc()))
                    .As("RankInCategory"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("ROW_NUMBER() OVER (PARTITION BY [Products].[CategoryId] ORDER BY [Products].[Price] DESC)");
    }

    [Fact]
    public void Rank_GeneratesRankFunction()
    {
        var query = Sql
            .From(EmployeesTable)
            .Select(
                Employees.Name,
                Employees.Salary,
                Window.Rank()
                    .Over(o => o.OrderBy(Employees.Salary.Desc()))
                    .As("SalaryRank"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("RANK() OVER (ORDER BY [Employees].[Salary] DESC)");
    }

    [Fact]
    public void DenseRank_GeneratesDenseRankFunction()
    {
        var query = Sql
            .From(EmployeesTable)
            .Select(
                Employees.Name,
                Window.DenseRank()
                    .Over(o => o.OrderBy(Employees.Salary.Desc()))
                    .As("DenseRank"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("DENSE_RANK() OVER");
    }

    [Fact]
    public void Ntile_GeneratesNtileFunction()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(
                Products.Name,
                Window.Ntile(4)
                    .Over(o => o.OrderBy(Products.Price.Desc()))
                    .As("PriceQuartile"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("NTILE(4) OVER");
    }

    [Fact]
    public void Lag_GeneratesLagFunction()
    {
        var query = Sql
            .From(OrdersTable)
            .Select(
                Orders.Id,
                Orders.Total,
                Window.Lag(Orders.Total)
                    .Over(o => o
                        .PartitionBy(Orders.CustomerId)
                        .OrderBy(Orders.OrderDate.Asc()))
                    .As("PreviousOrderTotal"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("LAG([Orders].[Total]) OVER");
    }

    [Fact]
    public void Lead_GeneratesLeadFunction()
    {
        var query = Sql
            .From(OrdersTable)
            .Select(
                Orders.Id,
                Orders.Total,
                Window.Lead(Orders.Total, 2)
                    .Over(o => o.OrderBy(Orders.OrderDate.Asc()))
                    .As("FutureOrderTotal"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("LEAD([Orders].[Total], 2) OVER");
    }

    [Fact]
    public void SumOver_GeneratesWindowAggregate()
    {
        var query = Sql
            .From(OrdersTable)
            .Select(
                Orders.Id,
                Orders.Total,
                Window.Sum(Orders.Total)
                    .Over(o => o
                        .PartitionBy(Orders.CustomerId)
                        .OrderBy(Orders.OrderDate.Asc()))
                    .As("RunningTotal"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("SUM([Orders].[Total]) OVER");
        sql.Should().Contain("PARTITION BY [Orders].[CustomerId]");
        sql.Should().Contain("ORDER BY [Orders].[OrderDate] ASC");
    }

    [Fact]
    public void FirstValue_GeneratesFirstValueFunction()
    {
        var query = Sql
            .From(OrdersTable)
            .Select(
                Orders.Id,
                Window.FirstValue(Orders.Total)
                    .Over(o => o
                        .PartitionBy(Orders.CustomerId)
                        .OrderBy(Orders.OrderDate.Asc()))
                    .As("FirstOrderTotal"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("FIRST_VALUE([Orders].[Total]) OVER");
    }
}
