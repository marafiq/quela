using FluentAssertions;
using Quela.Tests.Parsing;
using Xunit;
using static Quela.Tests.Db;

namespace Quela.Tests.Features;

public class BasicQueryTests
{
    [Fact]
    public void SimpleSelect_GeneratesValidSql()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(Products.Id, Products.Name, Products.Price);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("SELECT [Products].[Id], [Products].[Name], [Products].[Price]");
        sql.Should().Contain("FROM [dbo].[Products]");
    }

    [Fact]
    public void SelectAll_GeneratesStarSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .SelectAll();

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("SELECT *");
    }

    [Fact]
    public void SelectWithAlias_GeneratesAsSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .Select(Products.Name.As("ProductName"), Products.Price.As("UnitPrice"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("[Products].[Name] AS [ProductName]");
        sql.Should().Contain("[Products].[Price] AS [UnitPrice]");
    }

    [Fact]
    public void SelectDistinct_GeneratesDistinctKeyword()
    {
        var query = Sql
            .From(ProductsTable)
            .SelectDistinct(Products.Name);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("SELECT DISTINCT [Products].[Name]");
    }

    [Fact]
    public void SelectTop_GeneratesTopClause()
    {
        var query = Sql
            .From(ProductsTable)
            .SelectTop(10, Products.Id, Products.Name);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("SELECT TOP (10)");
    }

    [Fact]
    public void WhereWithEquals_ParameterizesValue()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Name == "Widget")
            .Select(Products.Id);

        var result = query.Build();

        SqlValidator.AssertValid(result.Sql);
        result.Sql.Should().Contain("WHERE [Products].[Name] = @p0");
        result.Parameters.Should().ContainKey("@p0");
        result.Parameters["@p0"].Should().Be("Widget");
    }

    [Fact]
    public void WhereWithNull_GeneratesIsNullSyntax()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.CategoryId == null)
            .Select(Products.Id);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("WHERE [Products].[CategoryId] IS NULL");
    }

    [Fact]
    public void MultipleWhereClauses_CombinedWithAnd()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Price > 100)
            .And(Products.Status == "Active")
            .Select(Products.Id);

        var result = query.Build();

        SqlValidator.AssertValid(result.Sql);
        result.Sql.Should().Contain("WHERE");
        result.Sql.Should().Contain("[Products].[Price] > @p0");
        result.Sql.Should().Contain("AND");
        result.Sql.Should().Contain("[Products].[Status] = @p1");
    }

    [Fact]
    public void WhereOr_GroupedCorrectly()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Price > 100)
            .Or(Products.Status == "Featured")
            .Select(Products.Id);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("OR");
    }

    [Fact]
    public void OrderByAsc_GeneratesAscKeyword()
    {
        var query = Sql
            .From(ProductsTable)
            .OrderBy(Products.Name.Asc())
            .Select(Products.Id, Products.Name);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("ORDER BY [Products].[Name] ASC");
    }

    [Fact]
    public void OrderByDesc_GeneratesDescKeyword()
    {
        var query = Sql
            .From(ProductsTable)
            .OrderBy(Products.Price.Desc())
            .Select(Products.Id, Products.Price);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("ORDER BY [Products].[Price] DESC");
    }

    [Fact]
    public void MultipleOrderBy_GeneratesCommaSeparatedList()
    {
        var query = Sql
            .From(ProductsTable)
            .OrderBy(Products.Name.Asc(), Products.Price.Desc())
            .Select(Products.Id, Products.Name, Products.Price);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("ORDER BY [Products].[Name] ASC, [Products].[Price] DESC");
    }

    [Fact]
    public void OffsetFetch_GeneratesPaginationClause()
    {
        var query = Sql
            .From(ProductsTable)
            .OrderBy(Products.Id.Asc())
            .Select(Products.Id, Products.Name)
            .OffsetFetch(10, 20);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY");
    }
}
