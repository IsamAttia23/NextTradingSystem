using NEXT.Application.Exceptions;

namespace NEXT.Application.Validation;

internal static class ValidationErrors
{
    public static void ThrowIfAny(IEnumerable<(string Field, string Message)> failures)
    {
        var errors = failures.GroupBy(x => x.Field, StringComparer.Ordinal).ToDictionary(
            group => group.Key, group => group.Select(x => x.Message).Distinct().ToArray(), StringComparer.Ordinal);
        if (errors.Count > 0) throw new AppValidationException(errors);
    }
}
