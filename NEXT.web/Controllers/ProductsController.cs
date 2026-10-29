using NEXT.Application.DTOs.Products;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class ProductsController(ISender sender, EntityPresenter<ProductDto, ProductInputDto> presenter)
    : EntityController<ProductDto, ProductInputDto, ProductQueryModel>(sender, presenter);
