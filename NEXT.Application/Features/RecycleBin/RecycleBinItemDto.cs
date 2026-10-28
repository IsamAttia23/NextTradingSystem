using System;

namespace NEXT.Application.Features.RecycleBin
{
    public class RecycleBinItemDto<TKey> where TKey : struct
    {
        public string EntityType { get; set; } = string.Empty;
        public TKey EntityId { get; set; }
        public string? DeletionReason { get; set; }
        public DateTimeOffset DeletedOn { get; set; }
    }
}
