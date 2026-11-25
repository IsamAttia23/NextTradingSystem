using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NEXT.Application.DTOs.Invoices;

namespace NEXT.web.Services;

public sealed class InvoiceDocumentHtml(IRazorViewEngine views, IModelMetadataProvider metadata,
    ITempDataProvider tempData)
{
    public async Task<string> RenderAsync(ActionContext action, InvoiceDetails invoice, CancellationToken ct)
        => await RenderViewAsync(action, "~/Views/Invoices/Print.cshtml", invoice, ct);

    public async Task<string> RenderViewAsync<T>(ActionContext action, string path, T model, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var view = views.GetView(null, path, true);
        if (!view.Success)
            throw new InvalidOperationException("The invoice document view is missing.");
        var data = new ViewDataDictionary<T>(metadata, new ModelStateDictionary())
        {
            Model = model, ["PdfExport"] = true
        };
        using var output = new StringWriter(System.Globalization.CultureInfo.CurrentCulture);
        var context = new ViewContext(action, view.View, data,
            new TempDataDictionary(action.HttpContext, tempData), output, new HtmlHelperOptions());
        await view.View.RenderAsync(context);
        ct.ThrowIfCancellationRequested();
        return output.ToString();
    }
}
