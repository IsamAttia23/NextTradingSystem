using NEXT.Application.DTOs.Tags;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class TagsController(ISender sender, EntityPresenter<TagDto, TagInputDto> presenter)
    : EntityController<TagDto, TagInputDto, PaginationQuery>(sender, presenter);
