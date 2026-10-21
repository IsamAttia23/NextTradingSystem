using NEXT.Domain.Common.QueryModels;
using NEXT.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Domain.QueryModels
{
    public class CustomerQueryModel : PaginationQuery
    {

        public CustomerType? Type { get; set; }

        public CustomerCategory? Category { get; set; }

        public string? TaxNumber { get; set; }

        public string? CommercialRegister { get; set; }

        public bool? IsActive { get; set; }

        public Guid? TagId { get; set; }

        public decimal? MinCreditLimit { get; set; }

        public decimal? MaxCreditLimit { get; set; }

        public decimal? MinCurrentBalance { get; set; }

        public decimal? MaxCurrentBalance { get; set; }

        public int? MinPaymentTermsDays { get; set; }

        public int? MaxPaymentTermsDays { get; set; }
    }
}
