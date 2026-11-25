using System.Security.Cryptography;
using System.Text.Json;

namespace NEXT.Application.Features.Invoices;

internal static class InvoiceOperationFingerprint
{
    public static string Of<T>(T input) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
}
