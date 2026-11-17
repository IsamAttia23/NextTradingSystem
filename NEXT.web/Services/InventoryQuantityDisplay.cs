using Microsoft.Extensions.Localization;
using NEXT.web.Localization;
using NEXT.web.Models;

namespace NEXT.web.Services;

public static class InventoryQuantityDisplay
{
    public static IReadOnlyList<FieldOption> UnitOptions { get; } =
        [new("false", "BaseStockUnit"), new("true", "PackagingUnitId")];

    public static string Format(decimal quantity, int? unitsPerPackage, string? baseUnit, string? packageUnit,
        IStringLocalizer<UiText> text)
    {
        var number = FormFieldPresenter.Quantity(quantity);
        if (string.IsNullOrEmpty(baseUnit))
        {
            return number;
        }
        var baseQuantity = text["QuantityWithUnit", number, baseUnit].Value;
        if (unitsPerPackage is not > 0 || string.IsNullOrEmpty(packageUnit) || quantity < unitsPerPackage)
        {
            return baseQuantity;
        }
        var packages = decimal.Truncate(quantity / unitsPerPackage.Value);
        var packagedQuantity = text["QuantityWithUnit", FormFieldPresenter.Quantity(packages), packageUnit].Value;
        var remainder = quantity % unitsPerPackage.Value;
        if (remainder > 0)
        {
            packagedQuantity = text["QuantityPlusRemainder", packagedQuantity,
                text["QuantityWithUnit", FormFieldPresenter.Quantity(remainder), baseUnit].Value].Value;
        }
        return text["PackagedQuantity", packagedQuantity, baseQuantity].Value;
    }
}
