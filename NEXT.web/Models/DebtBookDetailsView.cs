using NEXT.Application.DTOs.Accounting;
namespace NEXT.web.Models;

public sealed record DebtBookDetailsView(DebtBookStatement Statement, TableModel Movements);
