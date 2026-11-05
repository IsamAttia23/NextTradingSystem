using System.Text.Json;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Services.Currencies;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Currencies;

public sealed class SetExchangeRateHandler(
    ISystemSettingRepository settings,
    IProductRepository products,
    IQueryExecutor executor,
    IUnitOfWork unitOfWork,
    IEntityValidator<ExchangeRateInput> validator)
    : IRequestHandler<SetExchangeRateCommand, ExchangeRateDto>
{
    public async Task<ExchangeRateDto> Handle(SetExchangeRateCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request.Input);
        validator.Validate(request.Input);

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var setting = await settings.FindTrackedAsync(
                row => row.Key == ExchangeRateReader.SettingKey, token);

            if (setting is null)
            {
                // The first configured rate defines the conversion of all legacy SDG costs.
                await InitializeProductCostsAsync(request.Input.UsdToSdg, token);
                setting = new SystemSetting
                {
                    Key = ExchangeRateReader.SettingKey,
                    Category = "Currency"
                };
                await settings.AddAsync(setting, token);
            }
            else
            {
                settings.SetRowVersion(setting, request.Input.RowVersion);
            }

            setting.ValueJson = JsonSerializer.Serialize(request.Input.UsdToSdg);
            setting.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(token);
            return new ExchangeRateDto(request.Input.UsdToSdg, setting.RowVersion);
        }, ct);
    }

    private async Task InitializeProductCostsAsync(decimal rate, CancellationToken ct)
    {
        var existing = await executor.ToListAsync(products.QueryIncludingDeleted(), ct);
        foreach (var product in existing)
        {
            product.CostPrice = decimal.Round(product.CostPrice / rate, 8, MidpointRounding.AwayFromZero);
        }
    }
}
