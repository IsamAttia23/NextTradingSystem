using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Catalog;
public class CustomerContactInputDto
{
    [Required] public Guid CustomerId { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(100)] public string? Position { get; set; }
    [Phone, StringLength(32)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
}
public sealed class CustomerContactDto : CustomerContactInputDto
{
    public Guid Id { get; set; }

}
