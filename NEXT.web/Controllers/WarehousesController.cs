using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class WarehousesController(ICatalogService<WarehouseDto, WarehouseInputDto> service, CatalogPresenter<WarehouseDto, WarehouseInputDto> presenter)
    : CatalogController<WarehouseDto, WarehouseInputDto, WarehouseQueryModel>(service, presenter);
