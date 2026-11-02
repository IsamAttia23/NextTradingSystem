using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Presentation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Dashboard;

public sealed record GetDashboardQuery : IRequest<DashboardModel>;
