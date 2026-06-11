namespace AspireWebAppTemplate.Core.Contracts;

public sealed class RoleDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
    public bool IsDefault { get; set; }
    public bool RequiresMinimumUser { get; set; }
    public int Position { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
