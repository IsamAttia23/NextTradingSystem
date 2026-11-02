using System.Security.Claims;
using MediatR;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed record CheckApplicationUserPasswordQuery(Guid UserId, string Password) : IRequest<bool>;
