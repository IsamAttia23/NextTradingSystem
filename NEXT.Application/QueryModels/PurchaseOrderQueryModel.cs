
using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Application.QueryModels
{
    public sealed class PurchaseOrderQueryModel : PaginationQuery
    {
        // Date range
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }

        // Supplier filter
        public Guid? SupplierId { get; set; }

        // Purchase order status
        public PurchaseOrderStatus? Status { get; set; }
    }
}
