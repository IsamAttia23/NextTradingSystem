using NEXT.Application.DTOs.Common;
using NEXT.Application.Abstractions.Media;

using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

internal static class EntityInput
{
    public static void SetImage(object entity, object input, IProfileImageProcessor? images, byte[]? existing = null)
    {
        if (entity is not IHasProfileImage target || input is not IProfileImageInput source) return;
        target.AvatarUrl = source.RemoveAvatar ? null : source.AvatarUrl is { Length: > 0 }
            ? (images ?? throw new InvalidOperationException("Image processor is not registered.")).Normalize(source.AvatarUrl)
            : existing;
    }
}
