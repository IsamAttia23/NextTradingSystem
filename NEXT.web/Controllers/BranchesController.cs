using NEXT.Application.DTOs.Branches;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class BranchesController(ISender sender, EntityPresenter<BranchInputDto, BranchInputDto> presenter)
    : EntityController<BranchInputDto, BranchInputDto, PaginationQuery>(sender, presenter);
