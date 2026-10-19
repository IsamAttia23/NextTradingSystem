using System.ComponentModel.DataAnnotations;

namespace NEXT.Domain.Common
{
    public interface IEntity<TId> : TIdType<TId>
        where TId : struct
    {
        [Required]
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
