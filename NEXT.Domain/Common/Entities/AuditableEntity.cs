using System.ComponentModel.DataAnnotations;

namespace NEXT.Domain.Common.Entities
{
    public abstract class AuditableEntity<TId> : IEntity<TId>
        where TId : struct
    {
        public TId Id { get; set; }

        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public Guid? CreatedBy { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
