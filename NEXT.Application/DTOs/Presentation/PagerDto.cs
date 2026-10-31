using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Application.DTOs.Presentation
{
    public sealed record PagerDto(
        int Page,
        int TotalPages,
        int TotalCount,
        string PageKey = "PageNumber",
        int PageSize = 10,
        bool AllowPageSize = false
        );

}
