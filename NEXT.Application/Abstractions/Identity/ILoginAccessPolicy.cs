namespace NEXT.Application.Abstractions.Identity;

public interface ILoginAccessPolicy
{
    Task<bool> CanSignInAsync(Guid userId);
}
