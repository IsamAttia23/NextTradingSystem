using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class AccountFlowCookies(IHttpContextAccessor context, IDataProtectionProvider protection, TimeProvider clock, NEXT.Application.Abstractions.Context.ICurrentStore store)
{
    private const string RecoveryName = "NEXT.Recovery";
    private const string GrantName = "NEXT.PasswordGrant";

    public void SetRecovery(RecoveryFlow state) => Write(RecoveryName, state, TimeSpan.FromMinutes(15));
    public RecoveryFlow? GetRecovery() => Read<RecoveryFlow>(RecoveryName);
    public void SetGrant(PasswordFlowGrant state)
    {
        Write(GrantName, state, state.ExpiresAt - clock.GetUtcNow());
        Delete(RecoveryName);
    }
    public PasswordFlowGrant? GetGrant()
    {
        var grant = Read<PasswordFlowGrant>(GrantName);
        return grant is not null && grant.ExpiresAt > clock.GetUtcNow() ? grant : null;
    }
    public void Clear() { Delete(RecoveryName); Delete(GrantName); }

    private void Write<T>(string name, T state, TimeSpan lifetime) => context.HttpContext!.Response.Cookies.Append(
        Key(name), Protector(name).Protect(JsonSerializer.Serialize(state)),
        Options(lifetime));

    private T? Read<T>(string name)
    {
        if (!context.HttpContext!.Request.Cookies.TryGetValue(Key(name), out var value)) return default;
        try { return JsonSerializer.Deserialize<T>(Protector(name).Unprotect(value)); }
        catch (Exception error) when (error is CryptographicException or JsonException or FormatException)
        { Delete(name); return default; }
    }
    private void Delete(string name) => context.HttpContext!.Response.Cookies.Delete(Key(name), Options(TimeSpan.Zero));
    private string Key(string name) => store.IsDefault ? name : name + "." + store.Key;
    private IDataProtector Protector(string name) => store.IsDefault
        ? protection.CreateProtector("NEXT.AccountFlow.v1", name)
        : protection.CreateProtector("NEXT.AccountFlow.v1", store.Key, name);
    private CookieOptions Options(TimeSpan lifetime) => new()
    {
        HttpOnly = true, Secure = context.HttpContext!.Request.IsHttps, SameSite = SameSiteMode.Strict,
        IsEssential = true, Path = (context.HttpContext!.Request.PathBase.Value ?? "") + "/Account", MaxAge = lifetime
    };
}
