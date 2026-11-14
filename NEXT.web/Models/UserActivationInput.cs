namespace NEXT.web.Models;

public sealed class UserActivationInput
{
    public bool? IsActive { get; set; }
    public bool? ExpectedIsActive { get; set; }
    public bool? ExpectedIsLocked { get; set; }
}
