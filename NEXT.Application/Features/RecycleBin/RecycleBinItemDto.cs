using System;

namespace NEXT.Application.Features.RecycleBin
{
    public class RecycleBinItemDto
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public DateTimeOffset DeletedOn { get; set; }
    }
}
