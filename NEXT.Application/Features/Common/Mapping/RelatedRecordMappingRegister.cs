using System.Linq.Expressions;
using System.Reflection;
using Mapster;
using NEXT.Application.DTOs.Presentation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Common.Mapping;

/// <summary>Builds each related-record projection once from the read contract's allowed scalar fields.</summary>
public sealed class RelatedRecordMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        var register = typeof(RelatedRecordMappingRegister).GetMethod(nameof(RegisterProjection),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var type in typeof(Product).Assembly.GetTypes().Where(type =>
                     type.Namespace == typeof(Product).Namespace && !type.IsAbstract && !type.ContainsGenericParameters &&
                     type.GetProperty("Id")?.PropertyType == typeof(Guid) &&
                     type != typeof(ApplicationUser) && type != typeof(UserRole) && type != typeof(RolePermission)))
        {
            register.MakeGenericMethod(type).Invoke(null, [config]);
        }
    }

    private static void RegisterProjection<TEntity>(TypeAdapterConfig config)
    {
        var entity = Expression.Parameter(typeof(TEntity), "record");
        var dictionary = typeof(Dictionary<string, object?>);
        var add = dictionary.GetMethod(nameof(Dictionary<string, object?>.Add))!;
        var fields = typeof(TEntity).GetProperties().Where(property => RelatedRecordDto.FieldNames.Contains(property.Name));
        var values = Expression.ListInit(Expression.New(dictionary), fields.Select(property =>
            Expression.ElementInit(add, Expression.Constant(property.Name),
                Expression.Convert(Expression.Property(entity, property), typeof(object)))));
        var record = Expression.New(typeof(RelatedRecordDto).GetConstructors().Single(),
            Expression.Property(entity, "Id"), values);
        config.NewConfig<TEntity, RelatedRecordDto>()
            .MapWith(Expression.Lambda<Func<TEntity, RelatedRecordDto>>(record, entity));
    }
}
