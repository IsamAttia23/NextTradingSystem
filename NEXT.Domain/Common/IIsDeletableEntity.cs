namespace NEXT.Domain.Common
{
    public interface IIsDeletableEntity
    {
        public DateTimeOffset? DeletedAt { get; set; }
        public Guid? DeletedBy { get; set; }
        public string? DeletionReason { get; set; }
    }
}
