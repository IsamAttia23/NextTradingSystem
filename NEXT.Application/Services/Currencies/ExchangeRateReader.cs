using System.Text.Json;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;

namespace NEXT.Application.Services.Currencies;

// Shared lookup only. Product price calculations belong to the command/query handlers.
public sealed class ExchangeRateReader(ISystemSettingRepository settings)
{
    public const string SettingKey = "Currency.UsdToSdg";

    public async Task<decimal> GetUsdRateAsync(CancellationToken ct)
    {
        var setting = await settings.GetByKeyAsync(SettingKey, ct)
            ?? throw new UnprocessableEntityException("حدد سعر الدولار من إعدادات العملات أولًا.");

        return ReadRate(setting);
    }

    internal static decimal ReadRate(SystemSetting setting)
    {
        try
        {
            var rate = JsonSerializer.Deserialize<decimal>(setting.ValueJson);
            if (rate > 0 && rate <= 1_000_000_000_000m)
            {
                return rate;
            }
        }
        catch (JsonException)
        {
            // Report invalid persisted configuration through the application error contract.
        }

        throw new UnprocessableEntityException("سعر الدولار المحفوظ غير صالح.");
    }
}
