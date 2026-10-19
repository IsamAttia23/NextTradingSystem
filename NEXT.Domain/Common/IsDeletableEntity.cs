namespace NEXT.Domain.Common
{
    public class IsDeletableEntity<TId> : AuditableEntity<TId>, IIsDeletableEntity where TId : struct
    {
        public DateTimeOffset? DeletedAt { get; set; }
        public Guid? DeletedBy { get; set; }
        public string? DeletionReason { get; set; }
    }
}
