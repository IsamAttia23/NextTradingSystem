namespace NEXT.Domain.Common.QueryModels;

public class TPaginationQueryModel<TEntity>
    where TEntity : class
{
    public IList<TEntity> Data { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
