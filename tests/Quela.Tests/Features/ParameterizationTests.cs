using FluentAssertions;
using Quela.Tests.Parsing;
using Xunit;
using static Quela.Tests.Db;

namespace Quela.Tests.Features;

public class ParameterizationTests
{
    [Theory]
    [InlineData("Robert'; DROP TABLE Products;--")]
    [InlineData("' OR '1'='1")]
    [InlineData("1; DELETE FROM Products")]
    [InlineData("'; EXEC xp_cmdshell('format c:')--")]
    public void MaliciousInput_IsParameterized(string maliciousInput)
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Name == maliciousInput)
            .Select(Products.Id);

        var result = query.Build();

        // SQL should contain parameter placeholder, not the raw value
        result.Sql.Should().Contain("@p0");
        result.Sql.Should().NotContain(maliciousInput);
        result.Parameters["@p0"].Should().Be(maliciousInput);
    }

    [Fact]
    public void MultipleParameters_AreNumberedSequentially()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Name == "Widget")
            .And(Products.Price > 100)
            .And(Products.Status == "Active")
            .Select(Products.Id);

        var result = query.Build();

        result.Parameters.Should().ContainKey("@p0");
        result.Parameters.Should().ContainKey("@p1");
        result.Parameters.Should().ContainKey("@p2");
        result.Parameters["@p0"].Should().Be("Widget");
        result.Parameters["@p1"].Should().Be(100m);
        result.Parameters["@p2"].Should().Be("Active");
    }

    [Fact]
    public void InClause_ParameterizesValues()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Id.In(1, 2, 3, 4, 5))
            .Select(Products.Name);

        var result = query.Build();

        SqlValidator.AssertValid(result.Sql);
        result.Sql.Should().Contain("IN @p0");
        result.Parameters["@p0"].Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void EmptyInClause_GeneratesAlwaysFalse()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Id.In(Array.Empty<int>()))
            .Select(Products.Name);

        var result = query.Build();

        // Empty IN should generate 1=0 (always false)
        result.Sql.Should().Contain("1 = 0");
    }

    [Fact]
    public void BetweenClause_ParameterizesBothValues()
    {
        var query = Sql
            .From(ProductsTable)
            .Where(Products.Price.Between(10m, 100m))
            .Select(Products.Name);

        var result = query.Build();

        SqlValidator.AssertValid(result.Sql);
        result.Sql.Should().Contain("BETWEEN @p0 AND @p1");
        result.Parameters["@p0"].Should().Be(10m);
        result.Parameters["@p1"].Should().Be(100m);
    }

    [Fact]
    public void UnicodeStrings_PreserveEncoding()
    {
        var unicodeValue = "日本語テスト 中文测试 العربية";

        var query = Sql
            .From(ProductsTable)
            .Where(Products.Name == unicodeValue)
            .Select(Products.Id);

        var result = query.Build();

        result.Parameters["@p0"].Should().Be(unicodeValue);
    }

    [Fact]
    public void NullParameter_GeneratesIsNull()
    {
        string? nullValue = null;

        var query = Sql
            .From(ProductsTable)
            .Where(Products.Name == nullValue)
            .Select(Products.Id);

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("IS NULL");
        sql.Should().NotContain("= @p");
    }
}
