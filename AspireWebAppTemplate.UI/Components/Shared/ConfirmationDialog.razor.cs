using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A reusable confirmation dialog built on MudBlazor's <see cref="MudDialog"/>.
/// </summary>
/// <remarks>
/// Use <c>IDialogService</c> to show this dialog and await the result:
/// <code>
/// var parameters = new DialogParameters
/// {
///     { x => x.ContentText, "Are you sure you want to delete this record? This action is permanent and cannot be undone." },
///     { x => x.SubmitBtnText, "Delete" },
///     { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
///     { x => x.DialogIconColor, Color.Error }
/// };
///
/// var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
/// var dialog = DialogService.Show&lt;ConfirmationDialog&gt;("Confirm delete", parameters, options);
/// var result = await dialog.Result;
/// if (!result.Cancelled)
/// {
///     // proceed with delete
/// }
/// </code>
/// </remarks>
public partial class ConfirmationDialog : ComponentBase
{
    /// <summary>
    /// The cascading MudBlazor dialog instance used to control the lifecycle of this dialog.
    /// </summary>
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The primary text/content displayed in the body of the confirmation dialog.
    /// </summary>
    [Parameter] public string ContentText { get; set; } = null!;

    /// <summary>
    /// The label text for the confirm/submit button. Defaults to <c>"Submit"</c>.
    /// </summary>
    [Parameter] public string SubmitBtnText { get; set; } = "Submit";

    /// <summary>
    /// The MudBlazor icon name to display (e.g., <c>Icons.Material.Rounded.Info</c>).
    /// </summary>
    [Parameter] public string DialogIcon { get; set; } = Icons.Material.Rounded.Info;

    /// <summary>
    /// The color applied to both the icon and the confirm button (e.g., <see cref="Color.Primary"/>).
    /// </summary>
    [Parameter] public Color DialogIconColor { get; set; } = Color.Primary;

    /// <summary>
    /// Confirms the dialog and returns <c>true</c> as the data payload.
    /// </summary>
    private void Submit() => MudDialog.Close(DialogResult.Ok(true));

    /// <summary>
    /// Cancels the dialog without returning a payload.
    /// </summary>
    private void Cancel() => MudDialog.Cancel();
}
