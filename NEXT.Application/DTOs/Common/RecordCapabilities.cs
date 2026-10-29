namespace NEXT.Application.DTOs.Common;
public interface IProfileImageInput
{
    byte[]? AvatarUrl { get; set; }
    bool RemoveAvatar { get; set; }
}
public interface ITaggedRecordDto { string? TagName { get; set; } string? TagColor { get; set; } }
