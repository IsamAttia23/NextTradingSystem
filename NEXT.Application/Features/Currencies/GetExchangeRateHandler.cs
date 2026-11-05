using MediatR;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Services.Currencies;

namespace NEXT.Application.Features.Currencies;

public sealed class GetExchangeRateHandler(ISystemSettingRepository settings)
    : IRequestHandler<GetExchangeRateQuery, ExchangeRateDto>
{
    public async Task<ExchangeRateDto> Handle(GetExchangeRateQuery request, CancellationToken ct)
    {
        var setting = await settings.GetByKeyAsync(ExchangeRateReader.SettingKey, ct);
        if (setting is null)
        {
            return new(0, []);
        }

        return new(ExchangeRateReader.ReadRate(setting), setting.RowVersion);
    }
}
