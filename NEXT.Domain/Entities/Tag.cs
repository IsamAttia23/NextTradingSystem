using NEXT.Domain.Common.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Domain.Entities
{
    public class Tag : IsDeletableEntity<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        public IList<Customer> Customers { get; set; } = new List<Customer>();
        public IList<Supplier> Suppliers { get; set; } = new List<Supplier>();
    }
}
