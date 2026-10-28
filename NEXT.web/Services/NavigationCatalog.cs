namespace NEXT.web.Services;
public sealed record NavigationItem(string Controller, string Title, string Action = "Index");
public sealed record NavigationGroup(string Title, NavigationItem[] Items);
public static class NavigationCatalog
{
    public static IReadOnlyList<NavigationGroup> Groups { get; } = [
        new("OverviewGroup", [new("Home", "Dashboard")]),
        new("PartnersGroup", [new("Customers", "Customers"), new("CustomersContact", "CustomersContact"), new("Suppliers", "Suppliers"), new("Tags", "Tags")]),
        new("InventoryGroup", [new("Products", "Products"), new("Categories", "Categories"), new("Units", "Units"), new("Warehouses", "Warehouses"),
            new("Inventory", "Stocks"), new("Inventory", "StockMovements", "Movements"), new("Inventory", "ProductBatches", "Batches")]),
        new("OrganizationGroup", [new("Branches", "Branches")]),
        new("SecurityGroup", [new("Users", "Users"), new("Roles", "Roles"), new("Permissions", "Permissions"), new("Assignments", "Assignments")]),
        new("SystemGroup", [new("RecycleBin", "RecycleBin")])
    ];
}
