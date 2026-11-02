using System.Security.Claims;
using MediatR;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed record GetApplicationUserByEmailQuery(string Email) : IRequest<UserDto?>;
