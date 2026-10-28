namespace NEXT.Application.Abstractions.Identity;
public interface IUserDeletionPolicy
{
    Task ValidateAsync(Guid userId, CancellationToken ct);
}
