using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class CategoriesController(ICatalogService<CategoryDto, CategoryInputDto> service, CatalogPresenter<CategoryDto, CategoryInputDto> presenter)
    : CatalogController<CategoryDto, CategoryInputDto, CategoryQueryModel>(service, presenter);
