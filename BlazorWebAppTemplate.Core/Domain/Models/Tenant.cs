namespace BlazorWebAppTemplate.Core.Domain.Models;

/// <summary>
/// Represents a logical customer or organizational partition in a multi‑tenant application.
/// Each tenant owns its users, data, configuration, and permissions and must be isolated
/// from other tenants by application logic and data constraints.
/// </summary>
/// <remarks>
/// In a single‑tenant deployment, the application may operate without any tenant concept.
/// In a multi‑tenant deployment (shared database), a TenantId is typically stored on users
/// and domain entities, and queries are filtered by the current tenant to prevent data leakage.
/// </remarks>
public sealed class Tenant
{
    /// <summary>
    /// Unique identifier for the tenant (Guid).
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Human‑readable name of the tenant (e.g., company or department).
    /// </summary>
    public string Name { get; private set; } = "";

    /// <summary>
    /// Optional DNS domain used to resolve the tenant at runtime (e.g., contoso.example.com).
    /// </summary>
    public string? Domain { get; private set; }

    /// <summary>
    /// Indicates whether the tenant is active. Inactive tenants should be blocked from sign‑in or data access.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public Tenant(Guid id, string name, string? domain = null)
    {
        Id = id;
        Name = name;
        Domain = domain;
    }
}