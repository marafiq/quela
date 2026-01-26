namespace Quela.Tests;

/// <summary>
/// Test database schema for unit tests.
/// Simulates what the source generator would produce.
/// </summary>
public static class Db
{
    public static class Products
    {
        public static readonly Column<int> Id = new("Products", "Id");
        public static readonly Column<string> Name = new("Products", "Name");
        public static readonly Column<decimal> Price = new("Products", "Price");
        public static readonly Column<int> CategoryId = new("Products", "CategoryId");
        public static readonly Column<int?> NullableCategoryId = new("Products", "CategoryId");
        public static readonly Column<DateTime> CreatedAt = new("Products", "CreatedAt");
        public static readonly Column<string> Status = new("Products", "Status");
    }

    public static class Categories
    {
        public static readonly Column<int> Id = new("Categories", "Id");
        public static readonly Column<string> Name = new("Categories", "Name");
    }

    public static class Orders
    {
        public static readonly Column<int> Id = new("Orders", "Id");
        public static readonly Column<int> CustomerId = new("Orders", "CustomerId");
        public static readonly Column<DateTime> OrderDate = new("Orders", "OrderDate");
        public static readonly Column<decimal> Total = new("Orders", "Total");
        public static readonly Column<string> Status = new("Orders", "Status");
    }

    public static class OrderItems
    {
        public static readonly Column<int> Id = new("OrderItems", "Id");
        public static readonly Column<int> OrderId = new("OrderItems", "OrderId");
        public static readonly Column<int> ProductId = new("OrderItems", "ProductId");
        public static readonly Column<int> Quantity = new("OrderItems", "Quantity");
        public static readonly Column<decimal> Amount = new("OrderItems", "Amount");
    }

    public static class Customers
    {
        public static readonly Column<int> Id = new("Customers", "Id");
        public static readonly Column<string> Name = new("Customers", "Name");
        public static readonly Column<string> Email = new("Customers", "Email");
        public static readonly Column<DateTime> CreatedAt = new("Customers", "CreatedAt");
        public static readonly Column<string> Status = new("Customers", "Status");
    }

    public static class Employees
    {
        public static readonly Column<int> Id = new("Employees", "Id");
        public static readonly Column<string> Name = new("Employees", "Name");
        public static readonly Column<int?> ManagerId = new("Employees", "ManagerId");
        public static readonly Column<decimal> Salary = new("Employees", "Salary");
        public static readonly Column<string> Department = new("Employees", "Department");
    }

    // Table references for use in FROM clause
    public static readonly Table ProductsTable = new("Products", "dbo");
    public static readonly Table CategoriesTable = new("Categories", "dbo");
    public static readonly Table OrdersTable = new("Orders", "dbo");
    public static readonly Table OrderItemsTable = new("OrderItems", "dbo");
    public static readonly Table CustomersTable = new("Customers", "dbo");
    public static readonly Table EmployeesTable = new("Employees", "dbo");
}
