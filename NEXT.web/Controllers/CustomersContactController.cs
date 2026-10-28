using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class CustomersContactController(ICatalogService<CustomerContactDto, CustomerContactInputDto> service, CatalogPresenter<CustomerContactDto, CustomerContactInputDto> presenter)
    : CatalogController<CustomerContactDto, CustomerContactInputDto, CustomerContactQueryModel>(service, presenter);
