using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Settings;

public sealed class StoreProfileInput
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;
    [StringLength(200)]
    public string? Subtitle { get; set; }
    [StringLength(500)]
    public string? Address { get; set; }
    [StringLength(50)]
    public string? Phone { get; set; }
    [StringLength(50)]
    public string? SecondaryPhone { get; set; }
    public byte[]? RowVersion { get; set; }
}
