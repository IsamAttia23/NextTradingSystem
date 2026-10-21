using NEXT.Domain.Common.QueryModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Domain.QueryModels
{
    public class PaymentQueryModel : PaginationQuery
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public string? CustomerName { get; set; }
        public string? SupplierName { get; set; }
    }
}
