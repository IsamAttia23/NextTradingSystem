using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Categories;
public class CategoryInputDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
     public Guid? ParentId { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    [Url, StringLength(2048)] public string? ImageUrl { get; set; }
     public bool IsActive { get; set; } = true;
}
public sealed class CategoryDto : CategoryInputDto
{
    public Guid Id { get; set; }

}
