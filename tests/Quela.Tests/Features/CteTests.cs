using FluentAssertions;
using Quela.Tests.Parsing;
using Xunit;
using static Quela.Tests.Db;

namespace Quela.Tests.Features;

public class CteTests
{
    [Fact]
    public void SimpleCte_GeneratesWithClause()
    {
        var query = Sql
            .With("ExpensiveProducts", () => Sql
                .From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name, Products.Price))
            .From(Sql.Cte("ExpensiveProducts"))
            .SelectAll();

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("WITH [ExpensiveProducts] AS");
        sql.Should().Contain("FROM [ExpensiveProducts]");
    }

    [Fact]
    public void MultipleCtes_GeneratesCommaSeparatedCtes()
    {
        var query = Sql
            .With("ExpensiveProducts", () => Sql
                .From(ProductsTable)
                .Where(Products.Price > 100)
                .Select(Products.Id, Products.Name))
            .With("CheapProducts", () => Sql
                .From(ProductsTable)
                .Where(Products.Price <= 100)
                .Select(Products.Id, Products.Name))
            .From(Sql.Cte("ExpensiveProducts"))
            .SelectAll();

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("[ExpensiveProducts] AS");
        sql.Should().Contain("[CheapProducts] AS");
    }

    [Fact]
    public void RecursiveCte_GeneratesUnionAll()
    {
        var query = Sql
            .WithRecursive("OrgChart",
                anchor: () => Sql
                    .From(EmployeesTable)
                    .Where(Employees.ManagerId.IsNull())
                    .Select(Employees.Id, Employees.Name, Employees.ManagerId),
                recursive: () => Sql
                    .From(EmployeesTable)
                    .Join(Sql.Cte("OrgChart").As("o")).On(Employees.ManagerId == Sql.Col<int>("o.Id"))
                    .Select(Employees.Id, Employees.Name, Employees.ManagerId))
            .From(Sql.Cte("OrgChart"))
            .SelectAll();

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("WITH [OrgChart] AS");
        sql.Should().Contain("UNION ALL");
    }

    [Fact]
    public void CteWithSubqueryJoin_GeneratesValidSql()
    {
        var query = Sql
            .With("RecentOrders", () => Sql
                .From(OrdersTable)
                .Where(Orders.OrderDate > DateTime.Today.AddDays(-30))
                .Select(Orders.Id, Orders.CustomerId, Orders.Total))
            .From(CustomersTable)
            .Join(Sql.Cte("RecentOrders").As("ro")).On(Customers.Id == Sql.Col<int>("ro.CustomerId"))
            .Select(Customers.Name, Sql.Col<decimal>("ro.Total"));

        var sql = query.ToSql();

        SqlValidator.AssertValid(sql);
        sql.Should().Contain("WITH [RecentOrders] AS");
        sql.Should().Contain("INNER JOIN [RecentOrders] AS [ro]");
    }
}
