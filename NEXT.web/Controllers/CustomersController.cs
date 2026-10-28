using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class CustomersController(ICatalogService<CustomerDto, CustomerInputDto> service, CatalogPresenter<CustomerDto, CustomerInputDto> presenter)
    : CatalogController<CustomerDto, CustomerInputDto, CustomerQueryModel>(service, presenter);
