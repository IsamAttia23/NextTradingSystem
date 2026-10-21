using NEXT.Domain.Common.QueryModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Domain.QueryModels
{
    public class JournalEntryQueryModel : PaginationQuery
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
    }
}
