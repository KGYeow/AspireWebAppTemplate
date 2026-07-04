# Implementation Plan: Notification Push Deep Link

## Overview

Extend the real-time notification push pipeline to carry the notification ID (Guid) end-to-end—from API creation through SignalR delivery to the browser—so that the snackbar toast's click handler navigates the user directly to `/account/notifications?id={notificationId}`. The implementation touches four layers: the DTO, the callback endpoint, the hub context, and the notification bell UI component.

## Tasks

- [ ] 1. Extend NotificationPushRequest DTO and API service
  - [ ] 1.1 Add NotificationId property to NotificationPushRequest
    - Add `public Guid NotificationId { get; set; }` to the existing `NotificationPushRequest` class in `AspireWebAppTemplate.Core/Contracts/Notifications/NotificationPushRequest.cs`
    - Include XML documentation explaining the property's purpose (deep-link URL construction)
    - _Requirements: 1.1_

  - [ ] 1.2 Set NotificationId in NotificationService.CreateNotificationAsync
    - In `AspireWebAppTemplate.ApiService/Services/NotificationService.cs`, update the `NotificationPushRequest` construction to set `NotificationId = notification.Id` after persisting the entity
    - _Requirements: 1.2_

- [ ] 2. Update NotificationCallbackEndpoint validation and SignalR invocation
  - [ ] 2.1 Add Guid.Empty validation for NotificationId
    - In `AspireWebAppTemplate.Web/Endpoints/NotificationCallbackEndpoint.cs`, add validation: if `request.NotificationId == Guid.Empty`, return `Results.BadRequest("NotificationId is required.")`
    - Place alongside existing field validation in the `HandlePush` method
    - _Requirements: 1.3_

  - [ ] 2.2 Extend SignalR SendAsync to include notificationId as 5th parameter
    - Update the `SendAsync("ReceiveNotification", ...)` call to pass `request.NotificationId` as the 5th argument after `request.UnreadCount`
    - _Requirements: 2.1_

- [ ] 3. Create NotificationReceivedEventArgs and update NotificationContext
  - [ ] 3.1 Create NotificationReceivedEventArgs class
    - Create new file `AspireWebAppTemplate.Web/Models/NotificationReceivedEventArgs.cs`
    - Define sealed class with four `required init` properties: `Title` (string), `Message` (string), `Category` (string), `NotificationId` (Guid)
    - Include full XML documentation on class and all properties
    - _Requirements: 3.1_

  - [ ] 3.2 Update INotificationContext event signature
    - In `AspireWebAppTemplate.Web/Abstractions/INotificationContext.cs`, change the `OnNotificationReceived` event type from `Action<string, string, string>?` to `Action<NotificationReceivedEventArgs>?`
    - Update XML documentation to reference the new event args type
    - _Requirements: 3.2_

  - [ ] 3.3 Update NotificationContext hub handler to 5-param registration
    - In `AspireWebAppTemplate.Web/Services/Contexts/NotificationContext.cs`, update `_hubConnection.On<...>` registration to accept 5 parameters: `string, string, string, int, Guid`
    - Update the handler method to accept the `notificationId` parameter and raise `OnNotificationReceived` with a populated `NotificationReceivedEventArgs` instance
    - _Requirements: 2.2, 3.3_

- [ ] 4. Checkpoint - Verify pipeline compiles end-to-end
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Update NotificationBell event handler and snackbar deep link
  - [ ] 5.1 Update HandleNotificationReceived to accept NotificationReceivedEventArgs
    - In `AspireWebAppTemplate.Web/Components/Layout/Topbar/NotificationBell.razor.cs`, change `HandleNotificationReceived` method signature to accept `NotificationReceivedEventArgs args`
    - Extract `Title`, `Message`, `Category`, and `NotificationId` from the event args object
    - Update the dropdown prepend logic to use `args.NotificationId` as the `Id` for the new `NotificationDto`
    - _Requirements: 4.2_

  - [ ] 5.2 Update ShowToast to accept notificationId and configure deep link onclick
    - Add `Guid notificationId` parameter to the `ShowToast` method
    - Configure the snackbar's `Onclick` handler to call `NavigationManager.NavigateTo($"/account/notifications?id={notificationId}")`
    - Update the call site in `HandleNotificationReceived` to pass `args.NotificationId`
    - _Requirements: 4.1, 4.3_

- [ ] 6. Write property-based and unit tests
  - [ ]* 6.1 Write property test for push request carrying entity ID
    - **Property 1: Push request carries persisted entity ID**
    - **Validates: Requirements 1.2**
    - Create `AspireWebAppTemplate.Tests/Notifications/NotificationPushIdPropertyTests.cs`
    - Use FsCheck.Xunit with `[Property(MaxTest = 2)]`

  - [ ]* 6.2 Write property test for endpoint forwarding all parameters
    - **Property 2: Endpoint forwards all parameters to SignalR**
    - **Validates: Requirements 2.1**
    - Create `AspireWebAppTemplate.Tests/Notifications/EndpointForwardingPropertyTests.cs`
    - Use FsCheck.Xunit with `[Property(MaxTest = 2)]`

  - [ ]* 6.3 Write property test for hub event args population
    - **Property 3: Hub event parameters faithfully populate event args**
    - **Validates: Requirements 3.3**
    - Create `AspireWebAppTemplate.Tests/Notifications/NotificationContextEventArgsPropertyTests.cs`
    - Use FsCheck.Xunit with `[Property(MaxTest = 2)]`

  - [ ]* 6.4 Write property test for deep link URL construction
    - **Property 4: Deep link URL correctly encodes notification ID**
    - **Validates: Requirements 4.1, 4.3**
    - Create `AspireWebAppTemplate.Tests/Notifications/DeepLinkUrlPropertyTests.cs`
    - Use FsCheck.Xunit with `[Property(MaxTest = 2)]`

  - [ ]* 6.5 Write unit tests for NotificationCallbackEndpoint validation
    - Create `AspireWebAppTemplate.Tests/Notifications/NotificationCallbackEndpointValidationTests.cs`
    - Test that `Guid.Empty` NotificationId returns 400 with message "NotificationId is required."
    - _Requirements: 1.3_

- [ ] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The implementation language is C# (.NET 10) as established by the existing codebase

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "3.1"] },
    { "id": 1, "tasks": ["1.2", "3.2"] },
    { "id": 2, "tasks": ["2.1", "2.2", "3.3"] },
    { "id": 3, "tasks": ["5.1"] },
    { "id": 4, "tasks": ["5.2"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "6.4", "6.5"] }
  ]
}
```
