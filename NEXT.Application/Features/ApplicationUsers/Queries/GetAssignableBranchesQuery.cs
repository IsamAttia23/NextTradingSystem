using MediatR;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed record GetAssignableBranchesQuery(Guid? CurrentBranchId = null) : IRequest<IReadOnlyList<BranchOption>>;
