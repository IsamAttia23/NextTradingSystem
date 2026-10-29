using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Tags;
public class TagInputDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] public string Color { get; set; } = "#153d97";
}
public sealed class TagDto : TagInputDto
{
    public Guid Id { get; set; }

}
