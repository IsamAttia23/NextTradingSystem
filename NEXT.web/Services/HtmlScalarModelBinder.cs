using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace NEXT.web.Services;
/// <summary>HTML number/date controls submit invariant decimal numbers and ISO dates in both Arabic and English UIs.</summary>
public sealed class HtmlScalarModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type == typeof(decimal) || type == typeof(DateOnly) ? new HtmlScalarModelBinder(type) : null;
    }
    private sealed class HtmlScalarModelBinder(Type type) : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext context)
        {
            var value = context.ValueProvider.GetValue(context.ModelName);
            if (value == ValueProviderResult.None) return Task.CompletedTask;
            context.ModelState.SetModelValue(context.ModelName, value);
            if (string.IsNullOrWhiteSpace(value.FirstValue) && Nullable.GetUnderlyingType(context.ModelType) is not null)
                context.Result = ModelBindingResult.Success(null);
            else if (type == typeof(decimal) && decimal.TryParse(value.FirstValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                context.Result = ModelBindingResult.Success(number);
            else if (type == typeof(DateOnly) && DateOnly.TryParseExact(value.FirstValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                context.Result = ModelBindingResult.Success(date);
            else context.ModelState.TryAddModelError(context.ModelName, "The supplied number or date is invalid.");
            return Task.CompletedTask;
        }
    }
}
