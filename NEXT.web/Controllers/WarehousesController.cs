using NEXT.Application.DTOs.Warehouses;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class WarehousesController(ISender sender, EntityPresenter<WarehouseDto, WarehouseInputDto> presenter)
    : EntityController<WarehouseDto, WarehouseInputDto, PaginationQuery>(sender, presenter);
