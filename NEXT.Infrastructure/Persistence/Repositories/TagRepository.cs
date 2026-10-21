using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Infrastructure.Persistence.Repositories
{
    public sealed class TagRepository(NEXTDbContext context) : TRepository<Tag, Guid>(context), ITagRepository 
    {
    }
}
