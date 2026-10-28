using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models.Users;

public sealed class CreateUserInput
{
    public UserInput Profile { get; set; } = new();
    [Required, MinLength(10), StringLength(128)] public string Password { get; set; } = "";
}
