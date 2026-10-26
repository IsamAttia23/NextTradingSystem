using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.IRepositories
{
    public interface ITagRepository : IKeyRepository<Tag, Guid>
    {
    }
}
