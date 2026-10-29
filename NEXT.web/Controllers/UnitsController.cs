using NEXT.Application.DTOs.UnitsOfMeasure;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class UnitsController(ISender sender, EntityPresenter<UnitOfMeasureDto, UnitOfMeasureInputDto> presenter)
    : EntityController<UnitOfMeasureDto, UnitOfMeasureInputDto, PaginationQuery>(sender, presenter);
