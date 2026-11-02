namespace NEXT.Application.Exceptions;

public sealed class RecordRelationshipException : AppException
{
    public RecordRelationshipException(Exception? innerException = null)
        : base("The operation is blocked by a record relationship.",
            ApplicationErrorCode.RuleViolation, innerException)
    {
    }
}
