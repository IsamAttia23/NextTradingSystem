namespace NEXT.web.Services;
public static class UiIcons
{
    public static string For(string title) => title switch
    {
        "Users" or "Customers" => "users", "CustomersContact" => "contact", "Suppliers" => "truck",
        "Tags" => "tag", "Categories" => "grid", "Branches" => "branch", "Warehouses" => "warehouse",
        "Products" or "Stocks" => "box", "Units" => "ruler", "StockMovements" or "Transfer" => "transfer",
        "ProductBatches" => "calendar", "Roles" => "shield", "Permissions" => "key", "Assignments" or "AssignRole" => "link",
        "Delete" or "Purge" or "RecycleBin" => "trash", "Edit" => "edit", "Restore" => "restore",
        "Details" => "contact",
        "Receive" or "Create" or "CreateUser" or "CreateRole" or "CreatePermission" => "plus",
        "AssignPermission" => "link", "Dashboard" => "home", _ => "grid"
    };
}
