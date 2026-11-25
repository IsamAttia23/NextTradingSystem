using NEXT.Domain.Entities;
namespace NEXT.Domain.Services;

public static class InvoiceStockLines
{
    public static bool IsValid(InvoiceItem line) => line.ProductId == Guid.Empty ? false :
        line.ProductId.HasValue ? line.ManualName is null : !string.IsNullOrWhiteSpace(line.ManualName) &&
        line.ManualName.Length <= 200 && line.UnitCost == 0 && line.ProductBatchId is null;
}
