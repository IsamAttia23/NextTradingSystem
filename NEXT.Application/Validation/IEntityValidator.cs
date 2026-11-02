namespace NEXT.Application.Validation;

public interface IEntityValidator<in TEntity> where TEntity : class
{
    void Validate(TEntity entity);
}
