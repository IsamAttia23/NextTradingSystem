using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.QueryModels;
using NEXT.Application.Common.QueryModels;
using MediatR;
using NEXT.Application.Abstractions.IServices;

using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class SuppliersController(ISender sender, EntityPresenter<SupplierDto, SupplierInputDto> presenter)
    : EntityController<SupplierDto, SupplierInputDto, SupplierQueryModel>(sender, presenter);
