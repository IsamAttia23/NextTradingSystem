using System.ComponentModel.DataAnnotations;

namespace NEXT.Domain.Common.Entities
{
    public interface TIdType<TId>
        where TId : struct
    {
        [Required]
        public TId Id { get; set; }
    }
}
