namespace NEXT.Application.Abstractions.Context;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
