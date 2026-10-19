namespace NEXT.Infrastructure.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
