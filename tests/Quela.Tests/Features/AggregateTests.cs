using FluentAssertions;
using Quela.Tests.Parsing;
using Xunit;
using static Quela.Tests.Db;

namespace Quela.Tests.Features;

public class AggregateTests
{
    [Fact]
    public void Count_GeneratesCountStar()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(Fn.Count().As("TotalProducts"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("COUNT(*) AS [TotalProducts]");
    }

    [Fact]
    public void CountColumn_GeneratesCountColumn()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(Fn.Count(Products.CategoryId).As("ProductsWithCategory"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("COUNT([Products].[CategoryId])");
    }

    [Fact]
    public void CountDistinct_GeneratesCountDistinct()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(Fn.CountDistinct(Products.CategoryId).As("UniqueCategories"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("COUNT(DISTINCT [Products].[CategoryId])");
    }

    [Fact]
    public void Sum_GeneratesSumFunction()
    {
        var query = Sql
            .From(OrderItemsTable)
            .Select(Fn.Sum(OrderItems.Amount).As("TotalAmount"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("SUM([OrderItems].[Amount])");
    }

    [Fact]
    public void Avg_GeneratesAvgFunction()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(Fn.Avg(Products.Price).As("AveragePrice"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("AVG([Products].[Price])");
    }

    [Fact]
    public void MinMax_GeneratesMinMaxFunctions()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(
                Fn.Min(Products.Price).As("MinPrice"),
                Fn.Max(Products.Price).As("MaxPrice"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("MIN([Products].[Price])");
        sql.Should().Contain("MAX([Products].[Price])");
    }

    [Fact]
    public void GroupBy_GeneratesGroupByClause()
    {
        var query = Sql
            .From(ProductsTable)
            .Join(CategoriesTable).On(Products.CategoryId == Categories.Id)
            .GroupBy(Categories.Name)
            .Select(Categories.Name, Fn.Count().As("ProductCount"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("GROUP BY [Categories].[Name]");
    }

    [Fact]
    public void GroupByMultiple_GeneratesCommaSeparatedGroupBy()
    {
        var query = Sql
            .From(ProductsTable)
            .GroupBy(Products.CategoryId, Products.Status)
            .Select(Products.CategoryId, Products.Status, Fn.Count().As("Count"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("GROUP BY [Products].[CategoryId], [Products].[Status]");
    }

    [Fact]
    public void Having_GeneratesHavingClause()
    {
        var query = Sql
            .From(ProductsTable)
            .GroupBy(Products.CategoryId)
            .Having(Fn.Count() > 5)
            .Select(Products.CategoryId, Fn.Count().As("ProductCount"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("HAVING COUNT(*) > @p0");
    }

    [Fact]
    public void HavingWithSum_GeneratesCorrectCondition()
    {
        var query = Sql
            .From(OrderItemsTable)
            .GroupBy(OrderItems.OrderId)
            .Having(Fn.Sum(OrderItems.Amount) > 1000)
            .Select(OrderItems.OrderId, Fn.Sum(OrderItems.Amount).As("Total"));

        var result = query.Build();

        SqlValidator.AssertValid(result.Sql);
        result.Sql.Should().Contain("HAVING SUM([OrderItems].[Amount]) > @p0");
        result.Parameters["@p0"].Should().Be(1000m);
    }
}
