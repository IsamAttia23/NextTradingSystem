using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for InvoiceItem; inherits common CRUD behavior from the generic repository.</summary>
public sealed class InvoiceItemRepository(NEXTDbContext context) : KeyRepository<InvoiceItem, Guid>(context), IInvoiceItemRepository
{

}
