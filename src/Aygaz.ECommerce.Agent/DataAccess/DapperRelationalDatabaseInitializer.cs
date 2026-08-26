using System.Data.Common;
using Dapper;

namespace Aygaz.ECommerce.Agent.DataAccess;

public sealed class DapperRelationalDatabaseInitializer(
    Func<DbConnection> connectionFactory,
    RelationalDatabaseProvider provider) : IRelationalDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = connectionFactory();
        await connection.OpenAsync(cancellationToken);

        foreach (string statement in GetSchemaStatements())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                statement,
                cancellationToken: cancellationToken));
        }
    }

    private IReadOnlyList<string> GetSchemaStatements()
    {
        if (provider == RelationalDatabaseProvider.SqlServer)
        {
            return
            [
                "IF OBJECT_ID('Customers', 'U') IS NULL CREATE TABLE Customers (Id INT NOT NULL PRIMARY KEY, FirstName NVARCHAR(100) NOT NULL, LastName NVARCHAR(100) NOT NULL, Email NVARCHAR(254) NOT NULL UNIQUE, Phone NVARCHAR(30) NULL, Address NVARCHAR(200) NULL, City NVARCHAR(100) NULL, CreatedAt DATETIME2 NOT NULL);",
                "IF OBJECT_ID('Products', 'U') IS NULL CREATE TABLE Products (Id INT NOT NULL PRIMARY KEY, Sku NVARCHAR(100) NOT NULL UNIQUE, Name NVARCHAR(200) NOT NULL, Category NVARCHAR(100) NOT NULL, UnitPrice DECIMAL(18,2) NOT NULL, IsActive BIT NOT NULL, CreatedAt DATETIME2 NOT NULL);",
                "IF OBJECT_ID('CustomerOrders', 'U') IS NULL CREATE TABLE CustomerOrders (Id INT NOT NULL PRIMARY KEY, OrderNumber NVARCHAR(100) NOT NULL UNIQUE, CustomerId INT NOT NULL, OrderDate DATETIME2 NOT NULL, Status NVARCHAR(20) NOT NULL, TotalAmount DECIMAL(18,2) NOT NULL, CONSTRAINT FK_CustomerOrders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id));",
                "IF OBJECT_ID('InventoryRecords', 'U') IS NULL CREATE TABLE InventoryRecords (Id INT NOT NULL PRIMARY KEY, ProductId INT NOT NULL, LocationCode NVARCHAR(50) NOT NULL, LocationName NVARCHAR(100) NOT NULL, QuantityAvailable INT NOT NULL, ReorderLevel INT NOT NULL, UpdatedAt DATETIME2 NOT NULL, CONSTRAINT FK_InventoryRecords_Products FOREIGN KEY (ProductId) REFERENCES Products(Id), CONSTRAINT UQ_InventoryRecords_Product_Location UNIQUE (ProductId, LocationCode));",
                "IF OBJECT_ID('OrderItems', 'U') IS NULL CREATE TABLE OrderItems (Id INT NOT NULL PRIMARY KEY, CustomerOrderId INT NOT NULL, ProductId INT NOT NULL, Quantity INT NOT NULL, UnitPrice DECIMAL(18,2) NOT NULL, CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (CustomerOrderId) REFERENCES CustomerOrders(Id), CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id), CONSTRAINT UQ_OrderItems_Order_Product UNIQUE (CustomerOrderId, ProductId));"
            ];
        }

        return
        [
            "CREATE TABLE IF NOT EXISTS Customers (Id INTEGER NOT NULL PRIMARY KEY, FirstName TEXT NOT NULL, LastName TEXT NOT NULL, Email TEXT NOT NULL UNIQUE, Phone TEXT NULL, Address TEXT NULL, City TEXT NULL, CreatedAt TEXT NOT NULL);",
            "CREATE TABLE IF NOT EXISTS Products (Id INTEGER NOT NULL PRIMARY KEY, Sku TEXT NOT NULL UNIQUE, Name TEXT NOT NULL, Category TEXT NOT NULL, UnitPrice NUMERIC NOT NULL, IsActive INTEGER NOT NULL, CreatedAt TEXT NOT NULL);",
            "CREATE TABLE IF NOT EXISTS CustomerOrders (Id INTEGER NOT NULL PRIMARY KEY, OrderNumber TEXT NOT NULL UNIQUE, CustomerId INTEGER NOT NULL, OrderDate TEXT NOT NULL, Status TEXT NOT NULL, TotalAmount NUMERIC NOT NULL, FOREIGN KEY (CustomerId) REFERENCES Customers(Id));",
            "CREATE TABLE IF NOT EXISTS InventoryRecords (Id INTEGER NOT NULL PRIMARY KEY, ProductId INTEGER NOT NULL, LocationCode TEXT NOT NULL, LocationName TEXT NOT NULL, QuantityAvailable INTEGER NOT NULL, ReorderLevel INTEGER NOT NULL, UpdatedAt TEXT NOT NULL, FOREIGN KEY (ProductId) REFERENCES Products(Id), UNIQUE (ProductId, LocationCode));",
            "CREATE TABLE IF NOT EXISTS OrderItems (Id INTEGER NOT NULL PRIMARY KEY, CustomerOrderId INTEGER NOT NULL, ProductId INTEGER NOT NULL, Quantity INTEGER NOT NULL, UnitPrice NUMERIC NOT NULL, FOREIGN KEY (CustomerOrderId) REFERENCES CustomerOrders(Id), FOREIGN KEY (ProductId) REFERENCES Products(Id), UNIQUE (CustomerOrderId, ProductId));"
        ];
    }
}
