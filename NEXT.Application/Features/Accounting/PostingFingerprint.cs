using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NEXT.Application.DTOs.Accounting;

namespace NEXT.Application.Features.Accounting;

internal static class PostingFingerprint
{
    public static string Of(PostingRequestDto input) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            input.Kind, input.ReferenceType, input.ReferenceId, input.InvoiceId, input.PaymentId,
            input.ReversesEntryId, input.Date, input.Description,
            Lines = input.Lines.Select(line => new { line.Account,
                Debit = line.Debit.ToString("F2", CultureInfo.InvariantCulture),
                Credit = line.Credit.ToString("F2", CultureInfo.InvariantCulture),
                line.CustomerId, line.DebtItemId, line.Description })
        })));
}
