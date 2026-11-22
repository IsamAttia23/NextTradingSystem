namespace NEXT.Application.DTOs.Accounting;

public sealed record ReverseEntryInput(Guid OperationId, Guid EntryId, DateOnly Date, string Reason);
