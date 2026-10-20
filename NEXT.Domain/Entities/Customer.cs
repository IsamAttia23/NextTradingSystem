using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Customer : IsDeletableEntity<Guid>
{
    public CustomerType Type { get; set; } = CustomerType.Individual;
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? TaxNumber { get; set; }
    public string? CommercialRegister { get; set; }
    public CustomerCategory Category { get; set; } = CustomerCategory.Retail;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public int PaymentTermsDays { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; }
    public byte[] RowVersion { get; set; }

    public ICollection<CustomerContact> Contacts { get; set; } = new List<CustomerContact>();
    public ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
}
