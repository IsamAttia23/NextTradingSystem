using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.CustomerContacts;

public class CustomerContactInputDto
{
    [NEXT.Application.Validation.NotEmptyGuid]
    public Guid? CustomerId { get; set; }
    [Required, StringLength(200)]
    public string Name { get; set; } = "";
    [StringLength(100)]
    public string? Position { get; set; }
    [Required, Phone, StringLength(32)]
    public string Phone { get; set; } = "";
    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }
}
