using MediatR;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed record GetAssignableBranchesQuery(Guid? CurrentBranchId = null) : IRequest<IReadOnlyList<BranchOption>>;

public sealed class GetAssignableBranchesHandler(IBranchLookup branches)
    : IRequestHandler<GetAssignableBranchesQuery, IReadOnlyList<BranchOption>>
{
    public async Task<IReadOnlyList<BranchOption>> Handle(GetAssignableBranchesQuery request, CancellationToken ct) =>
        (await branches.GetAsync(ct)).Where(x => x.IsActive || x.Id == request.CurrentBranchId).ToArray();
}
