namespace AspireWebAppTemplate.Core.Contracts;

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public int Position { get; set; }
}
