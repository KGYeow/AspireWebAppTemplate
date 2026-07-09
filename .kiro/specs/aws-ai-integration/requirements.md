# Requirements Document

## Introduction

This feature adds an AI integration layer to the application using Amazon Bedrock. The service allows authenticated users to send natural language prompts to an LLM hosted on Amazon Bedrock and receive generated text responses. The integration follows the existing thin controller / full service layer architecture, with DTOs in Core, an interface and implementation in ApiService, a REST endpoint via AiController, and a typed HttpClient in the Web project for Blazor pages to consume.

## Glossary

- **AI_Service**: The backend service implementation (`AiService`) that communicates with Amazon Bedrock to generate AI responses.
- **AI_Controller**: The thin REST API controller (`AiController`) that receives prompt requests and delegates to the AI_Service.
- **Api_AI_Client**: The typed HttpClient service (`ApiAiService`) in the Web project that calls the AI_Controller endpoint.
- **Bedrock_Runtime_Client**: The AWS SDK client (`AmazonBedrockRuntimeClient`) used to invoke foundation models on Amazon Bedrock.
- **Prompt_Request**: The DTO (`AiPromptRequest`) containing the user's natural language prompt text.
- **AI_Response**: The DTO (`AiResponseDto`) containing the generated text from the AI model.
- **Model_Identifier**: The Amazon Bedrock model ID string (e.g., `us.amazon.nova-2-lite-v1:0`) that identifies which foundation model to invoke. The `us.` prefix indicates the US cross-region inference profile.
- **Session_Credentials**: The temporary AWS credentials (Access Key ID + Secret Access Key + Session Token) used to authenticate with Amazon Bedrock. These credentials expire and must be refreshed periodically.

## Requirements

### Requirement 1: Send Prompt to AI Model

**User Story:** As an authenticated user, I want to send a natural language prompt to an AI model, so that I can receive AI-generated text responses within the application.

#### Acceptance Criteria

1. WHEN an authenticated user submits a Prompt_Request with non-empty prompt text of at most 10,000 characters, THE AI_Service SHALL send the prompt to the configured Bedrock foundation model and return an AI_Response containing the generated text within 60 seconds.
2. IF an authenticated user submits a Prompt_Request with empty or whitespace-only prompt text, THEN THE AI_Service SHALL throw an ArgumentException indicating the prompt text is required.
3. THE AI_Controller SHALL expose a POST endpoint at `api/ai/prompt` that accepts a Prompt_Request body and returns an AI_Response on success.
4. THE AI_Controller SHALL require authorization for the prompt endpoint.
5. IF the Bedrock foundation model does not respond within 60 seconds, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating the request timed out.
6. IF an authenticated user submits a Prompt_Request with prompt text exceeding 10,000 characters, THEN THE AI_Service SHALL throw an ArgumentException indicating the prompt text exceeds the maximum allowed length.

### Requirement 2: AWS Authentication and Configuration

**User Story:** As a developer, I want the AI service to use non-secret configuration (ModelId, Region) from appsettings.json and secret credentials from Aspire parameters, so that configuration follows ASP.NET Core conventions and secrets are never committed to source control.

#### Acceptance Criteria

1. WHEN all three credential configuration values (`Ai:AccessKeyId`, `Ai:SecretAccessKey`, and `Ai:SessionToken`) are present, THE AI_Service SHALL create a SessionAWSCredentials object and use it to authenticate the Bedrock_Runtime_Client.
2. WHEN only `Ai:AccessKeyId` and `Ai:SecretAccessKey` configuration values are present (no `Ai:SessionToken`), THE AI_Service SHALL create a BasicAWSCredentials object and use it to authenticate the Bedrock_Runtime_Client.
3. WHEN none of the credential configuration values (`Ai:AccessKeyId`, `Ai:SecretAccessKey`, `Ai:SessionToken`) are present, THE AI_Service SHALL fall back to the AWS SDK default credential chain (supporting IAM roles in production).
4. THE AI_Service SHALL read the Model_Identifier from `Ai:ModelId` in the base `appsettings.json` file, defaulting to `us.amazon.nova-2-lite-v1:0`. This is a non-secret application configuration value. The `us.` prefix is required for the US cross-region inference profile.
5. THE AI_Service SHALL read the AWS region from `Ai:Region` in the base `appsettings.json` file and configure the Bedrock_Runtime_Client to use that region. This is a non-secret application configuration value.
6. IF the AWS region configuration value is missing or empty, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating the region is not configured.
7. THE AI_Service SHALL receive credential values (`Ai:AccessKeyId`, `Ai:SecretAccessKey`, `Ai:SessionToken`) exclusively through Aspire parameters defined in the AppHost, which are passed to the ApiService as environment variables. These values SHALL NOT appear in any appsettings.json file.
8. IF the Bedrock_Runtime_Client throws an AmazonServiceException indicating expired or invalid Session_Credentials during invocation, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating the AWS credentials have expired and need to be refreshed, preserving the original exception as the inner exception.
9. THE AppHost project SHALL define three secret parameters (`ai-access-key-id`, `ai-secret-access-key`, `ai-session-token`) and pass them to the ApiService project as environment variables (`Ai__AccessKeyId`, `Ai__SecretAccessKey`, `Ai__SessionToken`).
10. THE project SHALL include developer documentation explaining: (a) how to obtain AWS credentials from the console (Option 3), (b) that Aspire prompts for secret parameter values on first run and stores them in User Secrets, (c) how to refresh expired session tokens via `dotnet user-secrets set`, and (d) that production environments use IAM roles with no explicit credentials.

### Requirement 3: Error Handling for Bedrock Communication

**User Story:** As a developer, I want clear error handling when the AI model is unavailable or returns errors, so that the application degrades gracefully.

#### Acceptance Criteria

1. IF the Bedrock_Runtime_Client returns a throttling error, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating rate limiting, preserving the original exception as the inner exception.
2. IF the Bedrock_Runtime_Client returns a model-not-found error, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating the configured model is unavailable, preserving the original exception as the inner exception.
3. IF the Bedrock_Runtime_Client returns a service-unavailable error, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating the AI service is temporarily unavailable, preserving the original exception as the inner exception.
4. IF the Bedrock_Runtime_Client throws an unexpected exception, THEN THE AI_Service SHALL log the exception at Error level and throw an InvalidOperationException with a message that does not expose internal exception details or stack traces, preserving the original exception as the inner exception.
5. THE AI_Controller SHALL map InvalidOperationException from the AI_Service to a 400 Bad Request response with the exception message as the response body.
6. IF the Bedrock_Runtime_Client returns a response with empty or null generated content, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating the model returned no content.
7. IF the Bedrock_Runtime_Client throws an exception indicating expired or invalid Session_Credentials, THEN THE AI_Service SHALL throw an InvalidOperationException with a message indicating the AWS credentials have expired and need to be refreshed, preserving the original exception as the inner exception.

### Requirement 4: Service Layer Interface and DI Registration

**User Story:** As a developer, I want the AI service to follow the existing interface-driven DI pattern, so that the integration is testable and consistent with the rest of the codebase.

#### Acceptance Criteria

1. THE AI_Service SHALL implement an `IAiService` interface located in `ApiService/Abstractions/`.
2. THE AI_Service implementation SHALL be located in `ApiService/Services/`.
3. THE AI_Service SHALL be registered as a scoped service via the existing `ApplicationServiceExtensions.AddApplicationServices()` method in the API project.
4. THE Bedrock_Runtime_Client SHALL be registered as a singleton in the DI container.

### Requirement 5: DTO Contracts

**User Story:** As a developer, I want DTOs for the AI feature to follow the existing contract conventions, so that the integration is consistent and discoverable.

#### Acceptance Criteria

1. THE Prompt_Request DTO SHALL be located in `Core/Contracts/Ai/` and contain a `Prompt` string property with a maximum length of 4000 characters.
2. THE AI_Response DTO SHALL be located in `Core/Contracts/Ai/` and contain a `GeneratedText` string property with a maximum length of 8000 characters.
3. THE Prompt_Request SHALL use data annotation validation to enforce that the `Prompt` property is required, non-empty, and does not exceed 4000 characters.
4. THE Prompt_Request and AI_Response DTOs SHALL follow existing Core DTO conventions: sealed class, XML documentation on all public properties, and string properties initialized to empty string defaults.

### Requirement 6: Web Project API Client

**User Story:** As a Blazor page developer, I want a typed HttpClient service for AI operations, so that I can call the AI endpoint following the established Web project patterns.

#### Acceptance Criteria

1. THE Api_AI_Client SHALL be located in `Web/Services/ApiClients/` and named `ApiAiService`.
2. THE Api_AI_Client SHALL expose a method that accepts a prompt string (maximum 4000 characters) and returns an `ApiResult<AiResponseDto>`.
3. THE Api_AI_Client SHALL be registered in `ApiClientServiceExtensions.AddApiClients()` with the Aspire service discovery base address (`https+http://apiservice`) and the `UserIdentityDelegatingHandler`.
4. IF the HTTP response from the AI endpoint indicates a non-success status code, THEN THE Api_AI_Client SHALL return a failed `ApiResult<AiResponseDto>` containing the response body as the error message.
5. IF the prompt string is null, empty, or whitespace, THEN THE Api_AI_Client SHALL return a failed `ApiResult<AiResponseDto>` with an error message indicating that the prompt is required.
