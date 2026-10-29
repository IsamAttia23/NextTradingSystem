using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.UnitsOfMeasure;
public class UnitOfMeasureInputDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(20)] public string Symbol { get; set; } = "";
     public Guid? BaseUnitId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999.999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal ConversionFactor { get; set; } = 1m;
}
public sealed class UnitOfMeasureDto : UnitOfMeasureInputDto
{
    public Guid Id { get; set; }

}
