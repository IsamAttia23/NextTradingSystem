namespace NEXT.Application.DTOs.Catalog;
public interface IProfileImageInput
{
    byte[]? AvatarUrl { get; set; }
    bool RemoveAvatar { get; set; }
}
public interface ITaggedRecordDto { string? TagName { get; set; } string? TagColor { get; set; } }
