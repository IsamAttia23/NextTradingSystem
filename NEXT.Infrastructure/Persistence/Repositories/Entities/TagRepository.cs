using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities
{
    public sealed class TagRepository(NEXTDbContext context) : KeyRepository<Tag, Guid>(context), ITagRepository 
    {
    }
}
