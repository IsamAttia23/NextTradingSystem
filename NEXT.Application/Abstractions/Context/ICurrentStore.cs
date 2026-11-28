namespace NEXT.Application.Abstractions.Context;

public interface ICurrentStore
{
    Guid Id { get; }
    string Key { get; }
    string? Slug { get; }
    string? Name { get; }
    bool IsDefault { get; }
}
