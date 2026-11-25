using MediatR;
using NEXT.Application.DTOs.Products;
using NEXT.Application.QueryModels;
namespace NEXT.Application.Features.Products;
public sealed record ProductReportQuery(ProductQueryModel Filters) : IRequest<IReadOnlyList<ProductDto>>;
