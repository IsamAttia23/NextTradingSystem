using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Catalog;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
namespace NEXT.Application.Services.Catalog;
internal static class ReferenceGuard
{
    public static async Task Require<T>(IRepository<T> repository, System.Linq.Expressions.Expression<Func<T,bool>> predicate,
        string field, CancellationToken ct) where T : class
    {
        if (!await repository.AnyAsync(predicate, ct)) Fail(field, "Select an existing, active record.");
    }
    public static void Fail(string field, string message) => throw new AppValidationException(new Dictionary<string,string[]> { [field] = [message] });
    public static async Task NoCycle<T>(Guid? id, Guid? parent, IKeyRepository<T, Guid> repository, Func<T, Guid?> next, string field, CancellationToken ct) where T : class
    {
        var visited = new HashSet<Guid>();
        while (parent is { } ancestor)
        {
            if (ancestor == id || !visited.Add(ancestor)) Fail(field, "A circular hierarchy is not allowed.");
            var item = await repository.GetByIdAsync(ancestor, ct) ?? throw new NotFoundException("Parent was not found.");
            parent = next(item);
        }
    }
}
public sealed class CustomerRules(IRepository<Tag> tags) : ICatalogRules<Customer, CustomerInputDto>
{
    public Task ValidateAsync(Guid? id, CustomerInputDto input, CancellationToken ct) => ReferenceGuard.Require(tags, x => x.Id == input.TagId, "TagId", ct);
}
public sealed class SupplierRules(IRepository<Tag> tags, IRepository<Supplier> suppliers) : ICatalogRules<Supplier, SupplierInputDto>
{
    public async Task ValidateAsync(Guid? id, SupplierInputDto input, CancellationToken ct)
    {
        await ReferenceGuard.Require(tags, x => x.Id == input.TagId, "TagId", ct);
        input.Code = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim();
        if (input.Code is not null && await suppliers.AnyAsync(x => x.Id != id && x.Code == input.Code, ct)) ReferenceGuard.Fail("Code", "Code already exists.");
    }
}
public sealed class CustomerContactRules(IRepository<Customer> customers) : ICatalogRules<CustomerContact, CustomerContactInputDto>
{
    public Task ValidateAsync(Guid? id, CustomerContactInputDto input, CancellationToken ct) => ReferenceGuard.Require(customers, x => x.Id == input.CustomerId && x.IsActive, "CustomerId", ct);
}
public sealed class CategoryRules(IKeyRepository<Category, Guid> categories) : ICatalogRules<Category, CategoryInputDto>
{
    public Task ValidateAsync(Guid? id, CategoryInputDto input, CancellationToken ct) => ReferenceGuard.NoCycle(id, input.ParentId, categories, x => x.ParentId, "ParentId", ct);
}
public sealed class UnitOfMeasureRules(IKeyRepository<UnitOfMeasure, Guid> units) : ICatalogRules<UnitOfMeasure, UnitOfMeasureInputDto>
{
    public Task ValidateAsync(Guid? id, UnitOfMeasureInputDto input, CancellationToken ct) => ReferenceGuard.NoCycle(id, input.BaseUnitId, units, x => x.BaseUnitId, "BaseUnitId", ct);
}
public sealed class WarehouseRules(IRepository<Branch> branches, IRepository<Warehouse> warehouses) : ICatalogRules<Warehouse, WarehouseInputDto>
{
    public async Task ValidateAsync(Guid? id, WarehouseInputDto input, CancellationToken ct)
    {
        if (input.BranchId is {} branch) await ReferenceGuard.Require(branches, x => x.Id == branch && x.IsActive, "BranchId", ct);
        if (input.IsDefault && !input.IsActive) ReferenceGuard.Fail("IsDefault", "A default warehouse must be active.");
        if (await warehouses.AnyAsync(x => x.Id != id && x.Code == input.Code, ct)) ReferenceGuard.Fail("Code", "Code already exists.");
        if (input.IsDefault && await warehouses.AnyAsync(x => x.Id != id && x.IsDefault, ct)) ReferenceGuard.Fail("IsDefault", "A default warehouse already exists.");
    }
}
public sealed class ProductRules(IRepository<Category> categories, IRepository<UnitOfMeasure> units, IRepository<Product> products) : ICatalogRules<Product, ProductInputDto>
{
    public async Task ValidateAsync(Guid? id, ProductInputDto input, CancellationToken ct)
    {
        if (input.CategoryId is {} category) await ReferenceGuard.Require(categories, x => x.Id == category && x.IsActive, "CategoryId", ct);
        if (input.UnitOfMeasureId is {} unit) await ReferenceGuard.Require(units, x => x.Id == unit, "UnitOfMeasureId", ct);
        if (input.MaxStock < input.MinStock) ReferenceGuard.Fail("MaxStock", "Maximum stock must be at least the minimum.");
        input.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();
        if (input.Barcode is not null && await products.AnyAsync(x => x.Id != id && x.Barcode == input.Barcode, ct)) ReferenceGuard.Fail("Barcode", "Barcode already exists.");
        if (id is {} productId && await products.AnyAsync(x => x.Id == productId && x.HasExpiry != input.HasExpiry && x.StockMovements.Any(), ct))
            ReferenceGuard.Fail("HasExpiry", "Expiry tracking cannot change after inventory movements exist.");
    }
}
