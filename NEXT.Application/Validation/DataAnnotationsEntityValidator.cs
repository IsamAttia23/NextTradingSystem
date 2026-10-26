using NEXT.Application.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.Validation;

/// <summary>Default validator for DataAnnotations; replace per entity to add domain rules.</summary>
public sealed class DataAnnotationsEntityValidator<TEntity> : IEntityValidator<TEntity>
    where TEntity : class
{
    public void Validate(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var validationContext = new ValidationContext(entity);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            entity,
            validationContext,
            validationResults,
            validateAllProperties: true);

        if (isValid)
            return;

        var errors = validationResults
            .SelectMany(result =>
                (result.MemberNames.Any() ? result.MemberNames : new[] { "Entity" })
                .Select(memberName => new
                {
                    MemberName = string.IsNullOrWhiteSpace(memberName) ? "Entity" : memberName,
                    ErrorMessage = result.ErrorMessage ?? "The supplied value is invalid."
                }))
            .GroupBy(item => item.MemberName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ErrorMessage).Distinct().ToArray(),
                StringComparer.Ordinal);

        throw new AppValidationException(errors);
    }
}
