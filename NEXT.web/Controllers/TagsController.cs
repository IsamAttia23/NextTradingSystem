using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class TagsController(ICatalogService<TagDto, TagInputDto> service, CatalogPresenter<TagDto, TagInputDto> presenter)
    : CatalogController<TagDto, TagInputDto, TagQueryModel>(service, presenter);
