using NEXT.Application.Exceptions;

namespace NEXT.Application.Validation;

internal static class RequestValidation
{
    public static void Identifier(Guid value, string field = "Id")
    {
        if (value == Guid.Empty)
        {
            Fail(field, "Select a valid identifier.");
        }
    }
    public static Guid Identifier(string? value, string field = "UserId")
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            Fail(field, "The identifier is invalid.");
        }
        return id;
    }
    public static string Text(string? value, string field, int maximum = 256)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > maximum)
        {
            Fail(field, "The supplied value is invalid.");
        }
        return text!;
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void Fail(string field, string message) =>
        throw new AppValidationException(new Dictionary<string, string[]>
        {
            [field] = [message]
        });
}
