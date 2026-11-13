namespace NEXT.Application.Features.Dashboard;

public interface IDashboardAccountCounts
{
    Task<(int Users, int ActiveUsers, int Roles)> ReadAsync(CancellationToken ct);
}
