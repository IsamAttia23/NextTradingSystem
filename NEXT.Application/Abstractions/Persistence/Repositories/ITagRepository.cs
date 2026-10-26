using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Persistence.Repositories
{
    public interface ITagRepository : IKeyRepository<Tag, Guid>
    {
    }
}
