namespace NEXT.Application.Features.Currencies;

public sealed record ExchangeRateDto(decimal UsdToSdg, byte[] RowVersion);
