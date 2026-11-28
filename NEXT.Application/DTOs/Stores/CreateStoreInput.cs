using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Stores;

public sealed class CreateStoreInput
{
    public Guid OperationId { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(40), RegularExpression("^[a-z0-9][a-z0-9-]{1,39}$")]
    public string? Slug { get; set; }
    [Required, StringLength(200)] public string OwnerName { get; set; } = "";
    [Required, StringLength(100)] public string OwnerUserName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string OwnerEmail { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 10)] public string OwnerPassword { get; set; } = "";
}
