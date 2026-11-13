using MediatR;
using NEXT.Application.DTOs.CustomerContacts;
using NEXT.Application.Exceptions;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class CustomerContactPresenter(ISender sender, TablePresenter tables, FormFieldPresenter fields)
    : EntityPresenter<CustomerContactDto, CustomerContactInputDto>(sender, tables, fields)
{
    protected override bool ShowActivity => false;

    protected override async Task<CustomerContactInputDto> CreateInputAsync(Guid? relatedId, CancellationToken ct)
    {
        if (relatedId is { } customerId)
        {
            var options = await Fields.OptionsAsync("Customers", ct);
            if (!options.Any(option => option.Value == customerId.ToString()))
            {
                throw new NotFoundException("Customer was not found.");
            }
        }
        return new() { CustomerId = relatedId };
    }

    protected override async Task<List<FormField>> BuildFormFieldsAsync(CustomerContactInputDto input, Guid? id, CancellationToken ct)
    {
        var form = await base.BuildFormFieldsAsync(input, id, ct);
        var customer = form.FindIndex(field => field.Name == nameof(CustomerContactInputDto.CustomerId));
        form[customer] = form[customer] with { Type = "reference" };
        return form;
    }
}
