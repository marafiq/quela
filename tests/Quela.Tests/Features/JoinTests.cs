using FluentAssertions;
using Quela.Tests.Parsing;
using Xunit;
using static Quela.Tests.Db;

namespace Quela.Tests.Features;

public class JoinTests
{
    [Fact]
    public void InnerJoin_GeneratesJoinSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .Join(CategoriesTable).On(Products.CategoryId == Categories.Id)
            .Select(Products.Name, Categories.Name.As("CategoryName"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("INNER JOIN [dbo].[Categories]");
        sql.Should().Contain("ON [Products].[CategoryId] = [Categories].[Id]");
    }

    [Fact]
    public void LeftJoin_GeneratesLeftOuterJoinSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .LeftJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
            .Select(Products.Name, Categories.Name.As("CategoryName"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("LEFT OUTER JOIN [dbo].[Categories]");
    }

    [Fact]
    public void RightJoin_GeneratesRightOuterJoinSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .RightJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
            .Select(Products.Name, Categories.Name.As("CategoryName"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("RIGHT OUTER JOIN [dbo].[Categories]");
    }

    [Fact]
    public void FullJoin_GeneratesFullOuterJoinSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .FullJoin(CategoriesTable).On(Products.CategoryId == Categories.Id)
            .Select(Products.Name, Categories.Name.As("CategoryName"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("FULL OUTER JOIN [dbo].[Categories]");
    }

    [Fact]
    public void CrossJoin_GeneratesCrossJoinWithoutOn()
    {
        var query = Sql
            .From(ProductsTable)
            .CrossJoin(CategoriesTable)
            .Select(Products.Name, Categories.Name.As("CategoryName"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("CROSS JOIN [dbo].[Categories]");
        sql.Should().NotContain("ON");
    }

    [Fact]
    public void MultipleJoins_ChainedCorrectly()
    {
        var query = Sql
            .From(OrderItemsTable)
            .Join(OrdersTable).On(OrderItems.OrderId == Orders.Id)
            .Join(ProductsTable).On(OrderItems.ProductId == Products.Id)
            .Select(Orders.Id, Products.Name, OrderItems.Quantity);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("INNER JOIN [dbo].[Orders]");
        sql.Should().Contain("INNER JOIN [dbo].[Products]");
    }

    [Fact]
    public void JoinWithTableAlias_UsesAliasInSql()
    {
        var p = ProductsTable.As("p");
        var c = CategoriesTable.As("c");

        var query = Sql
            .From(p)
            .Join(c).On(Products.CategoryId == Categories.Id)
            .Select(Products.Name, Categories.Name.As("CategoryName"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("[dbo].[Products] AS [p]");
        sql.Should().Contain("[dbo].[Categories] AS [c]");
    }

    [Fact]
    public void CrossApply_GeneratesApplySyntax()
    {
        var subquery = Sql
            .From(OrderItemsTable)
            .Where(OrderItems.OrderId == Orders.Id)
            .Select(Fn.Sum(OrderItems.Amount).As("Total"));

        var query = Sql
            .From(OrdersTable)
            .CrossApply(subquery, "t")
            .Select(Orders.Id, Sql.Col<decimal>("t.Total"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("CROSS APPLY");
        sql.Should().Contain("AS [t]");
    }

    [Fact]
    public void OuterApply_GeneratesOuterApplySyntax()
    {
        var subquery = Sql
            .From(OrderItemsTable)
            .Where(OrderItems.OrderId == Orders.Id)
            .Select(Fn.Sum(OrderItems.Amount).As("Total"));

        var query = Sql
            .From(OrdersTable)
            .OuterApply(subquery, "t")
            .Select(Orders.Id, Sql.Col<decimal>("t.Total"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("OUTER APPLY");
        sql.Should().Contain("AS [t]");
    }
}
