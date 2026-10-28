namespace NEXT.Application.Abstractions.Context;
public interface IApplicationInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}
