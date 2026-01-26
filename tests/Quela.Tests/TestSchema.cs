namespace Quela.Tests;

/// <summary>
/// Test database schema for unit tests.
/// Simulates what the source generator would produce.
/// </summary>
public static class Db
{
    public static class Products
    {
        public static readonly Table _Table = new("Products", "dbo");

        public static readonly Column<int> Id = new("Products", "Id");
        public static readonly Column<string> Name = new("Products", "Name");
        public static readonly Column<decimal> Price = new("Products", "Price");
        public static readonly Column<int?> CategoryId = new("Products", "CategoryId");
        public static readonly Column<DateTime> CreatedAt = new("Products", "CreatedAt");
        public static readonly Column<string> Status = new("Products", "Status");

        public static implicit operator Table(Products _) => _Table;
    }

    public static class Categories
    {
        public static readonly Table _Table = new("Categories", "dbo");

        public static readonly Column<int> Id = new("Categories", "Id");
        public static readonly Column<string> Name = new("Categories", "Name");

        public static implicit operator Table(Categories _) => _Table;
    }

    public static class Orders
    {
        public static readonly Table _Table = new("Orders", "dbo");

        public static readonly Column<int> Id = new("Orders", "Id");
        public static readonly Column<int> CustomerId = new("Orders", "CustomerId");
        public static readonly Column<DateTime> OrderDate = new("Orders", "OrderDate");
        public static readonly Column<decimal> Total = new("Orders", "Total");
        public static readonly Column<string> Status = new("Orders", "Status");

        public static implicit operator Table(Orders _) => _Table;
    }

    public static class OrderItems
    {
        public static readonly Table _Table = new("OrderItems", "dbo");

        public static readonly Column<int> Id = new("OrderItems", "Id");
        public static readonly Column<int> OrderId = new("OrderItems", "OrderId");
        public static readonly Column<int> ProductId = new("OrderItems", "ProductId");
        public static readonly Column<int> Quantity = new("OrderItems", "Quantity");
        public static readonly Column<decimal> Amount = new("OrderItems", "Amount");

        public static implicit operator Table(OrderItems _) => _Table;
    }

    public static class Customers
    {
        public static readonly Table _Table = new("Customers", "dbo");

        public static readonly Column<int> Id = new("Customers", "Id");
        public static readonly Column<string> Name = new("Customers", "Name");
        public static readonly Column<string> Email = new("Customers", "Email");
        public static readonly Column<DateTime> CreatedAt = new("Customers", "CreatedAt");

        public static implicit operator Table(Customers _) => _Table;
    }

    public static class Employees
    {
        public static readonly Table _Table = new("Employees", "dbo");

        public static readonly Column<int> Id = new("Employees", "Id");
        public static readonly Column<string> Name = new("Employees", "Name");
        public static readonly Column<int?> ManagerId = new("Employees", "ManagerId");
        public static readonly Column<decimal> Salary = new("Employees", "Salary");
        public static readonly Column<string> Department = new("Employees", "Department");

        public static implicit operator Table(Employees _) => _Table;
    }

    // Convenience for referencing tables
    public static readonly Table ProductsTable = Products._Table;
    public static readonly Table CategoriesTable = Categories._Table;
    public static readonly Table OrdersTable = Orders._Table;
    public static readonly Table OrderItemsTable = OrderItems._Table;
    public static readonly Table CustomersTable = Customers._Table;
    public static readonly Table EmployeesTable = Employees._Table;
}
