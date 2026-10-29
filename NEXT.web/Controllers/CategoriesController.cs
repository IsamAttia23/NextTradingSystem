using NEXT.Application.DTOs.Categories;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class CategoriesController(ISender sender, EntityPresenter<CategoryDto, CategoryInputDto> presenter)
    : EntityController<CategoryDto, CategoryInputDto, PaginationQuery>(sender, presenter);
