using NEXT.Domain.Common;

namespace NEXT.Domain.Entities;

public class Category : IsDeletableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
