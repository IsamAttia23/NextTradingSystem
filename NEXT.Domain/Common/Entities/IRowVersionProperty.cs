using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Domain.Common.Entities
{
    public interface IRowVersionProperty
    {
        public byte[] RowVersion { get; set; }
    }
}
