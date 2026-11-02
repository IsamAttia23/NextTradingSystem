using MediatR;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetAssignableBranchesHandler(IBranchLookup branches)
    : IRequestHandler<GetAssignableBranchesQuery, IReadOnlyList<BranchOption>>
{
    public async Task<IReadOnlyList<BranchOption>> Handle(GetAssignableBranchesQuery request, CancellationToken ct) =>
        (await branches.GetAsync(ct)).Where(x => x.IsActive || x.Id == request.CurrentBranchId).ToArray();
}
