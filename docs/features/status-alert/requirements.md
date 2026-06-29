# Requirements Document

## Introduction

A reusable `StatusAlert` Blazor component for the shared UI library (`AspireWebAppTemplate.UI`) that encapsulates the repeated MudAlert patterns found across 30+ usage sites. The component eliminates conditional-rendering boilerplate, enforces consistent styling, and supports all existing alert variations (dismissible, non-dismissible, dense, rich content) through a unified API.

## Glossary

- **StatusAlert**: The reusable Blazor component that wraps MudBlazor's `MudAlert` with self-hiding behavior and consistent defaults.
- **Message**: The string parameter bound to the alert content. When null or empty, the component renders nothing.
- **Severity**: A MudBlazor enum (`Severity.Error`, `Severity.Success`, `Severity.Info`, `Severity.Warning`) that controls the alert's color and icon.
- **Dismissible_Mode**: The configuration where the alert displays a close icon that invokes a callback to clear the message.
- **Dense_Mode**: A compact rendering variant intended for use within dialogs where vertical space is limited.
- **ChildContent**: A Blazor `RenderFragment` that allows rich markup (HTML, components) to be rendered inside the alert instead of plain text.
- **Consumer**: A Blazor page or component that uses the StatusAlert component.

## Requirements

### Requirement 1: Self-Hiding Rendering

**User Story:** As a developer, I want the StatusAlert to render nothing when its message is null or empty, so that I do not need to wrap every usage in a conditional `@if` block.

#### Acceptance Criteria

1. WHEN the Message parameter is null, THE StatusAlert SHALL render no markup to the DOM.
2. WHEN the Message parameter is an empty string, THE StatusAlert SHALL render no markup to the DOM.
3. WHEN the Message parameter is a non-empty string, THE StatusAlert SHALL render the alert with the message content.
4. WHEN the Message parameter changes from a non-empty value to null, THE StatusAlert SHALL remove the alert from the DOM.
5. WHEN ChildContent is provided and Message is null or empty, THE StatusAlert SHALL render no markup to the DOM.
6. WHEN ChildContent is provided and Message is a non-empty value, THE StatusAlert SHALL render the alert with the ChildContent markup.

### Requirement 2: Severity Support

**User Story:** As a developer, I want to specify a severity level for the alert, so that the component renders the appropriate visual styling (color, icon) for the type of message.

#### Acceptance Criteria

1. THE StatusAlert SHALL accept a Severity parameter of type `MudBlazor.Severity`.
2. THE StatusAlert SHALL default the Severity parameter to `Severity.Error` when no value is specified by the Consumer.
3. WHEN a Severity value is provided, THE StatusAlert SHALL pass the value to the underlying MudAlert component.

### Requirement 3: Dismissible Mode

**User Story:** As a developer, I want to make alerts dismissible with a close icon, so that users can acknowledge and clear status messages.

#### Acceptance Criteria

1. WHEN the Dismissible parameter is true, THE StatusAlert SHALL display a close icon on the alert.
2. WHEN the user clicks the close icon, THE StatusAlert SHALL invoke the MessageChanged EventCallback with a null value.
3. WHEN the Dismissible parameter is false, THE StatusAlert SHALL not display a close icon.
4. THE StatusAlert SHALL default the Dismissible parameter to true.

### Requirement 4: Consistent Styling

**User Story:** As a developer, I want the StatusAlert to apply consistent CSS classes across all usages, so that alert presentation is uniform without manual class specification at each call site.

#### Acceptance Criteria

1. THE StatusAlert SHALL apply the CSS class `border-1` to the underlying MudAlert.
2. WHEN the Dense parameter is false, THE StatusAlert SHALL apply the CSS class `mb-4` for bottom margin spacing.
3. WHEN the Dense parameter is true, THE StatusAlert SHALL not apply the `mb-4` CSS class.
4. WHEN the Consumer provides an additional Class parameter, THE StatusAlert SHALL append the Consumer-provided classes to the default classes.

### Requirement 5: Dense Mode

**User Story:** As a developer, I want a dense rendering mode for alerts within dialogs, so that the component uses less vertical space in confined layouts.

#### Acceptance Criteria

1. WHEN the Dense parameter is true, THE StatusAlert SHALL render the MudAlert with the Dense property enabled.
2. WHEN the Dense parameter is false, THE StatusAlert SHALL render the MudAlert without the Dense property.
3. THE StatusAlert SHALL default the Dense parameter to false.

### Requirement 6: Rich Content Support

**User Story:** As a developer, I want to render rich markup inside the alert (bold text, links, nested components), so that alert messages are not limited to plain strings.

#### Acceptance Criteria

1. WHEN ChildContent is provided, THE StatusAlert SHALL render the ChildContent RenderFragment inside the alert body.
2. WHEN ChildContent is not provided, THE StatusAlert SHALL render the Message string as the alert body text.
3. WHEN both ChildContent and Message are provided, THE StatusAlert SHALL render the ChildContent (ChildContent takes precedence over Message for body content).

### Requirement 7: Two-Way Binding Support

**User Story:** As a developer, I want to use `@bind-Message` syntax with the StatusAlert, so that dismissing the alert automatically clears the bound variable in the parent component.

#### Acceptance Criteria

1. THE StatusAlert SHALL expose a Message parameter of type `string?`.
2. THE StatusAlert SHALL expose a MessageChanged EventCallback of type `EventCallback<string?>`.
3. WHEN the close icon is clicked, THE StatusAlert SHALL invoke MessageChanged with null, enabling `@bind-Message` to clear the bound field in the Consumer.

### Requirement 8: Component Location

**User Story:** As a developer, I want the StatusAlert component in the shared UI library, so that it is available to all projects that reference AspireWebAppTemplate.UI.

#### Acceptance Criteria

1. THE StatusAlert SHALL reside in the `AspireWebAppTemplate.UI` project under the `Components/Shared` directory.
2. THE StatusAlert SHALL follow the partial class pattern with separate `.razor` and `.razor.cs` files.
3. THE StatusAlert SHALL be in the `AspireWebAppTemplate.UI.Components.Shared` namespace.
