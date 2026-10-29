using NEXT.Application.DTOs.Customers;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class CustomersController(ISender sender, EntityPresenter<CustomerDto, CustomerInputDto> presenter)
    : EntityController<CustomerDto, CustomerInputDto, CustomerQueryModel>(sender, presenter);
