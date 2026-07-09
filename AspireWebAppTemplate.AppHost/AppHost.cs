var builder = DistributedApplication.CreateBuilder(args);

// Shared secret for internal service-to-service authentication (API→Web callbacks).
var internalApiKey = builder.AddParameter("InternalApiKey", secret: true);

// AWS AI credentials — Aspire prompts for values on first run and stores in User Secrets.
var aiAccessKeyId = builder.AddParameter("ai-access-key-id", secret: true);
var aiSecretAccessKey = builder.AddParameter("ai-secret-access-key", secret: true);
var aiSessionToken = builder.AddParameter("ai-session-token", secret: true);

var apiService = builder.AddProject<Projects.AspireWebAppTemplate_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("INTERNAL_API_KEY", internalApiKey)
    .WithEnvironment("Ai__AccessKeyId", aiAccessKeyId)
    .WithEnvironment("Ai__SecretAccessKey", aiSecretAccessKey)
    .WithEnvironment("Ai__SessionToken", aiSessionToken);

var webfrontend = builder.AddProject<Projects.AspireWebAppTemplate_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("INTERNAL_API_KEY", internalApiKey);

// Enable API→Web service discovery for internal notification callbacks.
apiService.WithReference(webfrontend);

builder.Build().Run();
