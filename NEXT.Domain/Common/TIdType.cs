using System.ComponentModel.DataAnnotations;

namespace NEXT.Domain.Common
{
    public interface TIdType<TId>
        where TId : struct
    {
        [Required]
        public TId Id { get; set; }
    }
}
