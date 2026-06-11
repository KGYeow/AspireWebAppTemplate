using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A flexible, reusable modal dialog built on MudBlazor's <see cref="MudDialog"/>.
/// </summary>
/// <remarks>
/// <para>
/// The dialog renders a title (with optional divider), a content area (<see cref="ContentArea"/>),
/// and an optional actions/footer bar. Actions can be:
/// </para>
/// <list type="bullet">
///   <item><description>Automatically generated (default <c>Cancel</c> / <c>OK</c>), or</description></item>
///   <item><description>Supplied via <see cref="Actions"/>, or</description></item>
///   <item><description>Fully overridden using <see cref="ActionsTemplate"/>.</description></item>
/// </list>
/// <para>
/// Use <see cref="ReturnActionAsResult"/> to control what data is returned through
/// <c>DialogResult.Ok(Data)</c> when an action closes the dialog.
/// </para>
/// </remarks>
public partial class ModalDialog : ComponentBase
{
    /// <summary>
    /// Provides access to the MudBlazor dialog instance for controlling dialog behavior (close, cancel).
    /// </summary>
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// Overrides the entire header area. If set, this takes precedence over TitleContent and Title.
    /// </summary>
    [Parameter] public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    /// The title text displayed in the dialog header.
    /// </summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Determines whether a divider is shown below the title/header.
    /// </summary>
    [Parameter] public bool ShowDivider { get; set; } = true;

    /// <summary>
    /// The main content area of the dialog, provided as a RenderFragment.
    /// </summary>
    [Parameter] public RenderFragment ContentArea { get; set; } = null!;

    /// <summary>
    /// Determines whether the dialog footer with action buttons is displayed.
    /// </summary>
    [Parameter] public bool ShowActions { get; set; } = true;

    /// <summary>
    /// A collection of actions (buttons) to display in the dialog footer.
    /// If null, default Cancel/OK actions are shown.
    /// </summary>
    [Parameter] public IEnumerable<DialogAction>? Actions { get; set; }

    /// <summary>
    /// A custom template for rendering dialog actions.
    /// Overrides the default or provided Actions collection.
    /// </summary>
    [Parameter] public RenderFragment? ActionsTemplate { get; set; }

    /// <summary>
    /// Event callback triggered when an action button is clicked.
    /// </summary>
    [Parameter] public EventCallback<DialogAction> OnActionClick { get; set; }

    /// <summary>
    /// If true, the clicked action (or its Value) is returned as DialogResult.Data when closing.
    /// </summary>
    [Parameter] public bool ReturnActionAsResult { get; set; } = true;

    /// <summary>
    /// The default set of dialog actions used when no explicit actions are provided.
    /// </summary>
    private static readonly IReadOnlyList<DialogAction> DefaultDialogActions = new[]
    {
        DialogAction.Cancel(),
        DialogAction.Ok(value: true)
    };

    /// <summary>
    /// Returns the actions to render: <c>Actions</c> if it has items; otherwise <see cref="DefaultDialogActions"/>.
    /// </summary>
    private IReadOnlyList<DialogAction> GetComputedActions()
    {
        if (Actions is null || !Actions.Any())
            return DefaultDialogActions;

        if (Actions is IReadOnlyList<DialogAction> ro)
            return ro;

        if (Actions is List<DialogAction> list)
            return list;

        return Actions.ToList();
    }

    /// <summary>
    /// Handles the click event for a dialog action button.
    /// </summary>
    private async Task OnActionClickedAsync(DialogAction action)
    {
        if (OnActionClick.HasDelegate)
            await OnActionClick.InvokeAsync(action);

        switch (action.CloseBehavior)
        {
            case DialogCloseBehavior.None:
                break;

            case DialogCloseBehavior.Cancel:
                MudDialog.Cancel();
                break;

            case DialogCloseBehavior.Ok:
                var data = ReturnActionAsResult ? action.Value ?? action : null;
                MudDialog.Close(DialogResult.Ok(data));
                break;
        }
    }

    /// <summary>
    /// Defines how the dialog should behave when an action button is clicked.
    /// </summary>
    public enum DialogCloseBehavior
    {
        /// <summary>No automatic closing; caller handles it manually.</summary>
        None = 0,
        /// <summary>Close with DialogResult.Ok(data).</summary>
        Ok = 1,
        /// <summary>Close with DialogResult.Canceled = true.</summary>
        Cancel = 2
    }

    /// <summary>
    /// Represents a dialog action (button) with text, style, and behavior.
    /// </summary>
    public class DialogAction
    {
        /// <summary>The text displayed on the button.</summary>
        public string Text { get; set; } = "";

        /// <summary>The color of the button.</summary>
        public Color Color { get; set; } = Color.Default;

        /// <summary>The visual variant of the button.</summary>
        public Variant Variant { get; set; } = Variant.Filled;

        /// <summary>Indicates whether the button is disabled.</summary>
        public bool Disabled { get; set; }

        /// <summary>Determines how the dialog should close when this action is clicked.</summary>
        public DialogCloseBehavior CloseBehavior { get; set; } = DialogCloseBehavior.Ok;

        /// <summary>If true, clicking the button closes the dialog.</summary>
        public bool CloseOnClick { get; set; } = true;

        /// <summary>Marks this action as the default (e.g., primary action).</summary>
        public bool IsDefault { get; set; }

        /// <summary>Optional payload returned via DialogResult.Data.</summary>
        public object? Value { get; set; }

        /// <summary>Creates a Cancel action button.</summary>
        public static DialogAction Cancel(string text = "Cancel") => new()
        {
            Text = text,
            Color = Color.Default,
            Variant = Variant.Outlined,
            CloseBehavior = DialogCloseBehavior.Cancel,
            Value = false
        };

        /// <summary>Creates an OK action button.</summary>
        public static DialogAction Ok(string text = "OK", object? value = null, bool isDefault = true, Color color = Color.Primary, Variant variant = Variant.Filled) => new()
        {
            Text = text,
            Color = color,
            Variant = variant,
            CloseBehavior = DialogCloseBehavior.Ok,
            IsDefault = isDefault,
            Value = value
        };

        /// <summary>Creates an action button that does not close the dialog automatically.</summary>
        public static DialogAction NoClose(string text, Color color = Color.Default, Variant variant = Variant.Filled) => new()
        {
            Text = text,
            Color = color,
            Variant = variant,
            CloseBehavior = DialogCloseBehavior.None
        };
    }
}
