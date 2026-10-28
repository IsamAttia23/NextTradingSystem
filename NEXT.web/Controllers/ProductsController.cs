using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class ProductsController(ICatalogService<ProductDto, ProductInputDto> service, CatalogPresenter<ProductDto, ProductInputDto> presenter)
    : CatalogController<ProductDto, ProductInputDto, ProductQueryModel>(service, presenter);
