using MediatR;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.Presentation;
using NEXT.Domain.Enums;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class CustomerPresenter(ISender sender, TablePresenter tables, FormFieldPresenter fields)
    : EntityPresenter<CustomerDto, CustomerInputDto>(sender, tables, fields)
{
    protected override async Task<List<FormField>> BuildFormFieldsAsync(CustomerInputDto input, Guid? id, CancellationToken ct)
    {
        var form = await base.BuildFormFieldsAsync(input, id, ct);
        var company = form.FindIndex(field => field.Name == nameof(CustomerInputDto.CompanyName));
        form[company] = form[company] with
        {
            MaxLength = 200,
            VisibleWhenField = nameof(CustomerInputDto.Type),
            VisibleWhenValue = nameof(CustomerType.Company)
        };
        return form;
    }

    protected override async Task<IReadOnlyList<DetailFieldDto>> BuildDetailsAsync(CustomerDto item, CancellationToken ct)
    {
        var details = await base.BuildDetailsAsync(item, ct);
        return item.Type == CustomerType.Individual
            ? details.Where(field => field.Label != nameof(CustomerInputDto.CompanyName)).ToArray()
            : details;
    }

    protected override UiAction RelatedCreateAction(Guid id, Func<string, object?, string> route) =>
        new("AddContact", route("Create", new { controller = "CustomersContact", customerId = id }));
}
