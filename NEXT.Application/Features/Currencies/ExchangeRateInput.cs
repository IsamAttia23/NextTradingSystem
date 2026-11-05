using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.Features.Currencies;

public sealed class ExchangeRateInput
{
    [Range(typeof(decimal), "0.00000001", "1000000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal UsdToSdg { get; set; }

    public byte[]? RowVersion { get; set; }
}
