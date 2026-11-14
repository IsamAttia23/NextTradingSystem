namespace NEXT.web.Models;

public sealed record UserActivationToggle(bool IsActive, string Url, string UserName, bool IsLocked);
