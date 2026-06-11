# Adding a Page

## File Structure

Create a new folder under `Components/Pages/`:

```
Components/Pages/{FeatureName}/
├── Index.razor
├── Index.razor.cs
└── (optional dialogs)
```

## Razor Template

```razor
@page "/{route}"
@using BlazorWebAppTemplate.Core.Common.Defaults
@attribute [Authorize]  // or [Authorize(Roles = "Admin")]

<PageTitle>Feature Name</PageTitle>
<PageHeader Title="Feature Name" />

<!-- Alert area -->
@if (!string.IsNullOrEmpty(StatusMessage))
{
    <MudAlert Severity="@(StatusMessage.StartsWith("Error") ? Severity.Error : Severity.Success)"
              Class="mb-4" ShowCloseIcon CloseIconClicked="() => StatusMessage = null">
        @StatusMessage
    </MudAlert>
}

<!-- Content sections -->
<MudPaper Class="pa-4 mb-4" Elevation="0">
    <MudText Typo="Typo.h6" Class="mb-3">Section Title</MudText>
    <!-- fields -->
</MudPaper>
```

## Code-Behind Template

```csharp
namespace BlazorWebAppTemplate.Components.Pages.{FeatureName};

[Authorize]
public partial class Index : ComponentBase
{
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    private ApplicationUser? User { get; set; }
    protected string? StatusMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        User = await UserManager.GetUserAsync(authState.User);
        if (User is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }
    }
}
```

## Data Grid Page Template

For admin pages with MudDataGrid, follow the pattern in UserManagement or RoleManagement:
- Inject data source service
- Use `MudDataGrid<TViewModel>` with `ServerData` callback
- Use `DataGridUtils<T>` for filtering/sorting/pagination
- Add SelectColumn for bulk actions
- Toolbar: search + filters + bulk action buttons

## Navigation

- Admin pages: Add to `DefaultNavigationProvider` in the Administration group
- User pages: Add to DropdownProfile menu
- Never add user-specific pages (Profile, Settings) to sidebar navigation
