using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class UnitsController(ICatalogService<UnitOfMeasureDto, UnitOfMeasureInputDto> service, CatalogPresenter<UnitOfMeasureDto, UnitOfMeasureInputDto> presenter)
    : CatalogController<UnitOfMeasureDto, UnitOfMeasureInputDto, UnitOfMeasureQueryModel>(service, presenter);
