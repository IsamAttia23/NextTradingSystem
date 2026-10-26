namespace NEXT.Application.Validation;

/// <summary>
/// Entity-level validation hook. Register a specialised implementation for entities
/// that require business validation beyond DataAnnotations.
/// </summary>
public interface IEntityValidator<in TEntity> where TEntity : class
{
    void Validate(TEntity entity);
}
