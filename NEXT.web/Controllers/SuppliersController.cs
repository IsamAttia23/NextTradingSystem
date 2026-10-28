using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class SuppliersController(ICatalogService<SupplierDto, SupplierInputDto> service, CatalogPresenter<SupplierDto, SupplierInputDto> presenter)
    : CatalogController<SupplierDto, SupplierInputDto, SupplierQueryModel>(service, presenter);
