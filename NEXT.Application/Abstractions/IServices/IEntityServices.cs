using NEXT.Domain.Common.Entities;
using NEXT.Domain.Common.QueryModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Application.Abstractions.IServices
{
    public interface IEntityServices<TId, TDto, TCreationDto, TUpdateDto>
        where TId : struct
        where TDto : class, TIdType<TId>
        where TCreationDto : class
        where TUpdateDto : class, TIdType<TId>
    {
        Task<TDto?> Details(TId id);

        Task<TPaginationQueryModel<TDto>> GetAll(PaginationQuery query);

        Task<TDto> Add(TCreationDto creationDTO);

        Task Delete(TId id);

        Task<TDto> Update(TId id, TUpdateDto updateDto);

        Task IsExists(TId id);
    }
}
