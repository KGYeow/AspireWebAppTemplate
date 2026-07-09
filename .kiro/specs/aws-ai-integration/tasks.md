# Implementation Plan: AWS AI Integration

## Overview

This plan implements a provider-agnostic AI text generation layer backed by Amazon Bedrock. The implementation follows the existing thin controller / full service layer architecture with DTOs in Core, service interface and implementation in ApiService, a thin REST controller, and a typed HttpClient in the Web project. AWS credentials flow through Aspire parameters as environment variables; non-secret config lives in appsettings.json.

## Tasks

- [x] 1. Create DTO contracts and add NuGet package
  - [x] 1.1 Create AiPromptRequest and AiResponseDto in Core/Contracts/Ai/
    - Create `AspireWebAppTemplate.Core/Contracts/Ai/` directory
    - Create `AiPromptRequest.cs` — sealed class with `[Required]` and `[StringLength(4000)]` on `Prompt` property, XML docs, `string.Empty` default
    - Create `AiResponseDto.cs` — sealed class with `[StringLength(8000)]` on `GeneratedText` property, XML docs, `string.Empty` default
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 1.2 Add AWSSDK.BedrockRuntime NuGet package to ApiService project
    - Add `AWSSDK.BedrockRuntime` package reference to `AspireWebAppTemplate.ApiService.csproj`
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 2. Implement IAiService interface and AiService
  - [x] 2.1 Create IAiService interface in ApiService/Abstractions/
    - Define `Task<AiResponseDto> SendPromptAsync(AiPromptRequest request)` method
    - Add XML documentation with exception tags for ArgumentException and InvalidOperationException
    - Use `#region` structure per coding standards
    - _Requirements: 4.1, 1.1_

  - [x] 2.2 Implement AiService in ApiService/Services/
    - Inject `AmazonBedrockRuntimeClient`, `IConfiguration`, `ILogger<AiService>`
    - Use traditional constructor with explicit field assignments and `#region Constructor`
    - Implement prompt validation: reject empty/whitespace (ArgumentException), reject >10000 chars (ArgumentException)
    - Read `Ai:ModelId` from config (default `amazon.nova-2-lite-v1:0`)
    - Construct `ConverseRequest` with user prompt as `UserMessage`
    - Invoke `ConverseAsync` with 60-second `CancellationToken` timeout
    - Extract text content from response; throw InvalidOperationException if empty/null
    - Handle expired credentials: catch `AmazonServiceException` with expired error codes, wrap in InvalidOperationException
    - Map known AWS exceptions (ThrottlingException, ResourceNotFoundException, ServiceUnavailableException) to InvalidOperationException with descriptive messages
    - Sanitize unexpected exceptions: log at Error, wrap in InvalidOperationException without leaking details
    - Use `#region Prompt Operations` and `#region Private Helpers`
    - _Requirements: 1.1, 1.2, 1.5, 1.6, 2.4, 2.5, 2.8, 3.1, 3.2, 3.3, 3.4, 3.6, 3.7_

  - [x] 2.3 Write property test: Valid prompt produces mapped response
    - **Property 1: Valid prompt produces mapped response**
    - **Validates: Requirements 1.1**
    - Create `AspireWebAppTemplate.Tests/AiIntegration/AiServicePropertyTests.cs`
    - Use FsCheck.Xunit with `[Property(MaxTest = 100)]`
    - Mock `AmazonBedrockRuntimeClient` to return arbitrary non-empty string
    - Assert `AiResponseDto.GeneratedText` equals mocked output for any valid 1–4000 char prompt
    - Tag: `// Feature: aws-ai-integration, Property 1: Valid prompt produces mapped response`

  - [x] 2.4 Write property test: Whitespace-only prompts are rejected
    - **Property 2: Whitespace-only prompts are rejected**
    - **Validates: Requirements 1.2**
    - Assert ArgumentException thrown for any whitespace-only string
    - Assert Bedrock client is never called
    - Tag: `// Feature: aws-ai-integration, Property 2: Whitespace-only prompts are rejected`

  - [x] 2.5 Write property test: Known AWS errors map to descriptive exceptions
    - **Property 3: Known AWS errors map to descriptive exceptions**
    - **Validates: Requirements 3.1, 3.2, 3.3**
    - For each known error type, mock Bedrock client to throw it
    - Assert InvalidOperationException with descriptive message and original as InnerException
    - Tag: `// Feature: aws-ai-integration, Property 3: Known AWS errors map to descriptive exceptions`

  - [x] 2.6 Write property test: Unexpected exceptions do not leak internal details
    - **Property 4: Unexpected exceptions do not leak internal details**
    - **Validates: Requirements 3.4**
    - Mock Bedrock client to throw arbitrary Exception with random message
    - Assert InvalidOperationException message does NOT contain original message or stack trace
    - Assert InnerException preserves original
    - Tag: `// Feature: aws-ai-integration, Property 4: Unexpected exceptions do not leak internal details`

  - [x] 2.7 Write property test: Expired credentials produce descriptive exception
    - **Property 8: Expired credentials produce descriptive exception**
    - **Validates: Requirements 2.8, 3.7**
    - Mock Bedrock client to throw AmazonServiceException with expired credential error code
    - Assert InvalidOperationException with credential expiry message and original as InnerException
    - Tag: `// Feature: aws-ai-integration, Property 8: Expired credentials produce descriptive exception`

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement AiController and DI registration
  - [x] 4.1 Implement AiController in ApiService/Controllers/
    - Extend `BaseController` with `[Route("api/ai")]` and `[Authorize]`
    - Add `[HttpPost("prompt")]` endpoint accepting `[FromBody] AiPromptRequest`
    - Delegate to `IAiService.SendPromptAsync()`
    - Map ArgumentException → BadRequest, InvalidOperationException → BadRequest
    - Use `#region Constructor` and `#region Prompt Operations`
    - _Requirements: 1.3, 1.4, 3.5_

  - [x] 4.2 Register AmazonBedrockRuntimeClient and AiService in DI
    - In `ApiService/Extensions/ApplicationServiceExtensions.cs`, add singleton registration for `AmazonBedrockRuntimeClient` with three-tier credential resolution
    - Register `IAiService` → `AiService` as scoped
    - Read `Ai:Region` from config (throw if missing), read credential env vars, resolve SessionAWSCredentials / BasicAWSCredentials / default chain
    - _Requirements: 2.1, 2.2, 2.3, 2.5, 2.6, 2.7, 4.2, 4.3, 4.4_

  - [x] 4.3 Write unit tests for credential resolution and controller behavior
    - Create `AspireWebAppTemplate.Tests/AiIntegration/AiServiceUnitTests.cs`
    - Test missing region throws InvalidOperationException
    - Test timeout scenario throws InvalidOperationException
    - Test empty model response throws InvalidOperationException
    - Test controller maps InvalidOperationException to 400
    - Test three-tier credential resolution (session, basic, default)
    - _Requirements: 2.1, 2.2, 2.3, 2.6, 1.5, 3.5, 3.6_

- [x] 5. Implement AppHost parameters and configuration
  - [x] 5.1 Add Aspire secret parameters to AppHost
    - Define `ai-access-key-id`, `ai-secret-access-key`, `ai-session-token` as secret parameters in `AppHost.cs`
    - Pass as environment variables `Ai__AccessKeyId`, `Ai__SecretAccessKey`, `Ai__SessionToken` to apiservice
    - _Requirements: 2.7, 2.9_

  - [x] 5.2 Add non-secret AI configuration to appsettings.json
    - Add `"Ai": { "ModelId": "amazon.nova-2-lite-v1:0", "Region": "us-east-1" }` section to `ApiService/appsettings.json`
    - _Requirements: 2.4, 2.5_

- [x] 6. Implement Web project API client
  - [x] 6.1 Create ApiAiService typed HttpClient in Web/Services/ApiClients/
    - Create `ApiAiService.cs` with traditional constructor, `HttpClient` field
    - Implement `SendPromptAsync(string prompt)` returning `ApiResult<AiResponseDto>`
    - Client-side validation: return failure if prompt is null/empty/whitespace
    - POST to `/api/ai/prompt`, handle success/failure response mapping
    - Use `#region Constructor` and `#region Prompt Operations`
    - _Requirements: 6.1, 6.2, 6.4, 6.5_

  - [x] 6.2 Register ApiAiService in Web DI extensions
    - Add HttpClient registration in `ApiClientServiceExtensions.AddApiClients()` with `https+http://apiservice` base address and `UserIdentityDelegatingHandler`
    - _Requirements: 6.3_

  - [x] 6.3 Write property test: DTO validation enforces prompt constraints
    - **Property 5: DTO validation enforces prompt constraints**
    - **Validates: Requirements 5.1, 5.3**
    - Use `Validator.TryValidateObject` on `AiPromptRequest` with arbitrary strings
    - Assert validation succeeds iff string is non-null, non-empty, ≤4000 chars
    - Tag: `// Feature: aws-ai-integration, Property 5: DTO validation enforces prompt constraints`

  - [x] 6.4 Write property test: HTTP error responses map to failed ApiResult
    - **Property 6: HTTP error responses map to failed ApiResult**
    - **Validates: Requirements 6.4**
    - Mock HttpClient to return non-success status with arbitrary body
    - Assert `ApiResult.Succeeded` is false and `Error` contains response body
    - Tag: `// Feature: aws-ai-integration, Property 6: HTTP error responses map to failed ApiResult`

  - [x] 6.5 Write property test: Client-side prompt validation rejects invalid input
    - **Property 7: Client-side prompt validation rejects invalid input**
    - **Validates: Requirements 6.5**
    - For null/empty/whitespace strings, assert `ApiResult.Succeeded` is false without HTTP call
    - Tag: `// Feature: aws-ai-integration, Property 7: Client-side prompt validation rejects invalid input`

- [x] 7. Create developer documentation
  - [x] 7.1 Create AWS credentials developer guide
    - Create `docs/guides/aws-ai-credentials.md`
    - Document how to obtain credentials from AWS console (Option 3)
    - Explain Aspire prompts for secret values on first run and stores in User Secrets
    - Show how to refresh expired session tokens via `dotnet user-secrets set`
    - Explain production uses IAM roles with no explicit credentials
    - _Requirements: 2.10_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- All Bedrock interactions are mocked in tests — no real AWS calls
- The `AmazonBedrockRuntimeClient` is registered as singleton; `AiService` is scoped (per-request)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "5.2"] },
    { "id": 2, "tasks": ["2.2", "5.1"] },
    { "id": 3, "tasks": ["2.3", "2.4", "2.5", "2.6", "2.7", "4.1", "4.2"] },
    { "id": 4, "tasks": ["4.3", "6.1", "6.3"] },
    { "id": 5, "tasks": ["6.2", "6.4", "6.5"] },
    { "id": 6, "tasks": ["7.1"] }
  ]
}
```
