using Mapster;
using MapsterMapper;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Services;
using NEXT.Domain.Common.Entities;
using NEXT.Domain.Common.QueryModels;
using NEXT.Application.Exceptions;
using NEXT.Application.Abstractions.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace NEXT.Application.Services
{
    public abstract class TEntityService<TEntity, TId, TDto, TCreationDto, TUpdateDto> :
     IEntityServices<TId, TDto, TCreationDto, TUpdateDto>

     where TEntity : class, IEntity<TId>
     where TId : struct
     where TDto : class, TIdType<TId>
     where TCreationDto : class
     where TUpdateDto : class, TIdType<TId>
    {
        private readonly IRepository<TEntity> _repository;
        private readonly IKeyRepository<TEntity, TId> _keyRepository;
        private readonly IUnitOfWork _unitOfWork;

        private readonly IPaginationServices _paginationServices;
        private readonly IMapper _mapper;

        public TEntityService(IRepository<TEntity> repository, IMapper mapper,
            IPaginationServices paginationServices,IUnitOfWork unitOfWork
            ,IKeyRepository<TEntity,TId> keyRepository)
        {
            _repository = repository;
            _paginationServices = paginationServices;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _keyRepository = keyRepository;
        }

        public virtual async Task<TDto> Add(TCreationDto creationDTO)
        {
            var entity = _mapper.Map<TEntity>(creationDTO);

            entity.CreatedAt = DateTimeOffset.Now;

            await _repository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            var result = _mapper.Map<TDto>(entity);

            return result;
        }

        public virtual async Task Delete(TId id)
        {
            var entity = await _keyRepository.GetByIdAsync(id)
                ?? throw new NotFoundException("The request entity dosen't exits");

            await HasActiveRelationsAsync(entity);

            await _keyRepository.DeleteAsync(entity);
            await _unitOfWork.SaveChangesAsync();
        }

        public virtual async Task<TPaginationQueryModel<TDto>> GetAll(PaginationQuery query)
        {
            var entities = _repository.GetAllAsync();

            var result = await _paginationServices.GetPaggedList(entities.ProjectToType<TDto>(), query);

            return result;
        }

        public virtual async Task<TDto?> Details(TId id)
        {
            var entity = await _keyRepository.GetByIdAsync(id);
            if (entity == null)
                throw new NotFoundException("The request entity dosen't exits");

            var result = _mapper.Map<TDto>(entity);

            return result;
        }

        public virtual async Task<TDto> Update(TId id, TUpdateDto updateDto)
        {
            var entity = await _keyRepository.GetByIdAsync(id);

            if (entity == null)
                throw new NotFoundException("The request entity dosen't exits");

            var mappedEntity = _mapper.Map(updateDto, entity);

            mappedEntity.UpdatedAt = DateTimeOffset.Now;

            await _repository.Update(mappedEntity);
            await _unitOfWork.SaveChangesAsync();

            var result = _mapper.Map<TDto>(mappedEntity);
            return result;
        }

        public async Task IsExists(TId id)
        {
            var isExists = await _keyRepository.IsExistsAsync(id);

            if (!isExists)
                throw new NotFoundException("The request entity dosen't exits");
        }

        public async Task HasActiveRelationsAsync(TEntity entity)
        {
            var hasRealation = await _repository.HasActiveRelationsAsync(entity);
            if (hasRealation)
                throw new UnprocessableEntityException("Cannot Delete : This Entity has a Realation with Another Entity");
        }

    }
}
