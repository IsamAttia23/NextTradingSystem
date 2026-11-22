using NEXT.Application.DTOs.Invoices;
using NEXT.Domain.Enums;

namespace NEXT.web.Models;

public sealed record InvoiceFulfillmentForm(InvoiceDetails Invoice, InvoiceStatus TargetStatus);
