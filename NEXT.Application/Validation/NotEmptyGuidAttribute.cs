using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.Validation;

public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("Select a valid identifier.") { }
    public override bool IsValid(object? value) => value is null || value is Guid id && id != Guid.Empty;
}
