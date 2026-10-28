using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class BranchesController(ICatalogService<BranchDto, BranchInputDto> service, CatalogPresenter<BranchDto, BranchInputDto> presenter)
    : CatalogController<BranchDto, BranchInputDto, BranchQueryModel>(service, presenter);
