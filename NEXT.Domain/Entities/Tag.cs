using NEXT.Domain.Common.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Domain.Entities
{
    public class Tag : IsDeletableEntity<Guid>
    {
        public string Name { get; set; }
        public string Color { get; set; }

        public IList<Customer> Customers { get; set; }
        public IList<Supplier> Suppliers { get; set; }
    }
}
