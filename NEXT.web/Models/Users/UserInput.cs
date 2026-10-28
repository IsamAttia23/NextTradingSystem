using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models.Users;

public sealed class UserInput
{
    public IFormFile? Avatar { get; set; }
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    [Required, StringLength(256)] public string UserName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Phone, StringLength(32)] public string? PhoneNumber { get; set; }
    public Guid BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}
