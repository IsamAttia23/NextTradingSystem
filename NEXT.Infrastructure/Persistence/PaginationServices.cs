using Mapster;
using System;
using System.Collections.Generic;
using System.Text;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.QueryModels;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Common.QueryModels;

namespace NEXT.Infrastructure.Persistence
{
    public class PaginationServices : IPaginationServices
    {
        public async Task<TPaginationQueryModel<T>> GetPaggedList<T>(IQueryable<T> entities, PaginationQuery query, CancellationToken cancellationToken = default) where T : class
        {
            var pageNumber = query?.PageNumber > 0 ? query.PageNumber : 1;
            var pageSize = query?.PageSize > 0 ? query.PageSize : 10;
            pageSize = Math.Min(pageSize, 50);

            ArgumentNullException.ThrowIfNull(entities);
            if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
                throw new NEXT.Application.Exceptions.AppValidationException(new Dictionary<string, string[]> {
                    [nameof(query.PageNumber)] = ["The requested page is too large."]
                });
            var totalCount = await entities.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            pageNumber = Math.Min(pageNumber, Math.Max(1, totalPages));

            var pagedList = await entities.Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var result = new TPaginationQueryModel<T>
            {
                Data = pagedList,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return result;
        }
    }
}
