using NEXT.Domain.Common.QueryModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Domain.Queries

{
    public sealed class AuditLogQueryModel : PaginationQuery
    {
        public DateTimeOffset? From { get; init; }

        public DateTimeOffset? To { get; init; }

        public Guid? UserId { get; init; }

        public string? EntityType { get; init; }
    }
}
