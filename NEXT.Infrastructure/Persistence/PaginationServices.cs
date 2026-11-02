using Mapster;
using System;
using System.Collections.Generic;
using System.Text;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.QueryModels;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;

namespace NEXT.Infrastructure.Persistence
{
    public class PaginationServices : IPaginationServices
    {
        public Task<TPaginationQueryModel<T>> GetPagedList<T>(IQueryable<T> entities, PaginationQuery query, CancellationToken cancellationToken = default) where T : class
            => GetPagedList(entities, source => source, query, cancellationToken);

        public async Task<TPaginationQueryModel<TResult>> GetPagedList<TEntity, TResult>(IQueryable<TEntity> entities,
            Func<IQueryable<TEntity>, IQueryable<TResult>> projection, PaginationQuery query, CancellationToken cancellationToken = default)
            where TEntity : class where TResult : class
        {
            var pageNumber = query?.PageNumber > 0 ? query.PageNumber : 1;
            var pageSize = query?.PageSize > 0 ? query.PageSize : 10;
            pageSize = Math.Min(pageSize, 50);

            ArgumentNullException.ThrowIfNull(entities);
            ArgumentNullException.ThrowIfNull(projection);
            if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
                throw new AppValidationException(new Dictionary<string, string[]> {
                    [nameof(query.PageNumber)] = ["The requested page is too large."]
                });
            var totalCount = await StorageErrors.ExecuteAsync(() => entities.CountAsync(cancellationToken));
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            pageNumber = Math.Min(pageNumber, Math.Max(1, totalPages));

            var pageEntities = entities.Skip((pageNumber - 1) * pageSize).Take(pageSize);
            var data = await StorageErrors.ExecuteAsync(() => projection(pageEntities).ToListAsync(cancellationToken));

            var pagedList = new TPaginationQueryModel<TResult>
            {
                Data = data,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return pagedList;
        }
    }
}
