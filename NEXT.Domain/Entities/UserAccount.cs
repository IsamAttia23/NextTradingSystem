namespace NEXT.Domain.Entities;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset? UpdatedOn { get; set; }
    public Guid BranchId { get; set; }
    public byte[]? AvatarUrl { get; set; }
    public string? DeletionReason { get; set; }
}
