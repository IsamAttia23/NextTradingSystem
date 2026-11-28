using System.Text.Json;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.DTOs.Settings;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Settings;

public sealed class StoreProfileSettings(ISystemSettingRepository settings, IUnitOfWork work,
    IEntityValidator<StoreProfileInput> validator, TimeProvider clock)
{
    public const string SettingKey = "Documents.StoreProfile";
    private StoreProfileInput? cached;

    public async Task<StoreProfileInput> ReadAsync(CancellationToken ct)
    {
        if (cached is not null) return cached;
        var row = await settings.GetByKeyAsync(SettingKey, ct);
        cached = row is null ? new() : JsonSerializer.Deserialize<StoreProfileInput>(row.ValueJson)
            ?? throw new InvalidOperationException("Invalid store document settings.");
        cached.RowVersion = row?.RowVersion;
        return cached;
    }

    public Task SaveAsync(StoreProfileInput input, CancellationToken ct)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        input.Subtitle = Clean(input.Subtitle);
        input.Address = Clean(input.Address);
        input.Phone = Clean(input.Phone);
        input.SecondaryPhone = Clean(input.SecondaryPhone);
        validator.Validate(input);
        return work.ExecuteInTransactionAsync(async token =>
        {
            var row = await settings.FindTrackedAsync(s => s.Key == SettingKey, token);
            if (row is null)
            {
                if (input.RowVersion is { Length: > 0 }) throw new ConflictException("StoreProfileChanged");
                row = new SystemSetting { Key = SettingKey, Category = "Documents" };
                await settings.AddAsync(row, token);
            }
            else
            {
                if (input.RowVersion is not { Length: > 0 } || !row.RowVersion.SequenceEqual(input.RowVersion))
                    throw new ConflictException("StoreProfileChanged");
                settings.SetRowVersion(row, input.RowVersion);
            }
            row.ValueJson = JsonSerializer.Serialize(new { input.Name, input.Subtitle, input.Address, input.Phone, input.SecondaryPhone });
            row.UpdatedAt = clock.GetUtcNow();
            await work.SaveChangesAsync(token);
            cached = null;
        }, ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
