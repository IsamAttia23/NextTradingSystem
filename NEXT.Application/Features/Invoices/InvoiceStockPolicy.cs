using Microsoft.Extensions.Configuration;
namespace NEXT.Application.Features.Invoices;
public sealed class InvoiceStockPolicy(IConfiguration configuration)
{
    public bool AllowOverselling => bool.TryParse(configuration["Inventory:AllowInvoiceOverselling"], out var enabled) && enabled;
}
