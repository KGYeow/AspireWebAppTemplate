var builder = DistributedApplication.CreateBuilder(args);

// Shared secret for internal service-to-service authentication (API→Web callbacks).
var internalApiKey = builder.AddParameter("InternalApiKey", secret: true);

var apiService = builder.AddProject<Projects.AspireWebAppTemplate_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("INTERNAL_API_KEY", internalApiKey);

var webfrontend = builder.AddProject<Projects.AspireWebAppTemplate_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("INTERNAL_API_KEY", internalApiKey);

// Enable API→Web service discovery for internal notification callbacks.
apiService.WithReference(webfrontend);

builder.Build().Run();
