using NEXT.Application.DTOs.CustomerContacts;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class CustomersContactController(ISender sender, EntityPresenter<CustomerContactDto, CustomerContactInputDto> presenter)
    : EntityController<CustomerContactDto, CustomerContactInputDto, PaginationQuery>(sender, presenter);
