using FluentAssertions;
using Quela.Tests.Parsing;
using Xunit;
using static Quela.Tests.Db;

namespace Quela.Tests.Features;

public class SetOperationTests
{
    [Fact]
    public void Union_GeneratesUnionClause()
    {
        var query1 = Sql
            .From(ProductsTable)
            .Where(Products.Price > 100)
            .Select(Products.Name.As("ItemName"));

        var query2 = Sql
            .From(ProductsTable)
            .Where(Products.Status == "Featured")
            .Select(Products.Name.As("ItemName"));

        var combined = query1.Union(query2).Select(Sql.All);

        var sql = combined.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("UNION");
        sql.Should().NotContain("UNION ALL");
    }

    [Fact]
    public void UnionAll_GeneratesUnionAllClause()
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

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("UNION ALL");
    }

    [Fact]
    public void Intersect_GeneratesIntersectClause()
    {
        var query1 = Sql
            .From(ProductsTable)
            .Where(Products.Price > 100)
            .Select(Products.Id);

        var query2 = Sql
            .From(ProductsTable)
            .Where(Products.Status == "Active")
            .Select(Products.Id);

        var combined = query1.Intersect(query2).Select(Sql.All);

        var sql = combined.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("INTERSECT");
    }

    [Fact]
    public void Except_GeneratesExceptClause()
    {
        var query1 = Sql
            .From(ProductsTable)
            .Select(Products.Id);

        var query2 = Sql
            .From(OrderItemsTable)
            .Select(OrderItems.ProductId);

        var combined = query1.Except(query2).Select(Sql.All);

        var sql = combined.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("EXCEPT");
    }

    [Fact]
    public void UnionWithOrderBy_OrdersEntireResult()
    {
        var query1 = Sql
            .From(ProductsTable)
            .Where(Products.Price > 100)
            .Select(Products.Name.As("ItemName"));

        var query2 = Sql
            .From(ProductsTable)
            .Where(Products.Status == "Featured")
            .Select(Products.Name.As("ItemName"));

        var combined = query1
            .Union(query2)
            .OrderBy(Sql.Col<string>("ItemName").Asc())
            .Select(Sql.All);

        var sql = combined.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("UNION");
        sql.Should().Contain("ORDER BY");
    }
}
