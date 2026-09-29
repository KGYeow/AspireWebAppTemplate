var builder = DistributedApplication.CreateBuilder(args);

// Shared secret for internal service-to-service authentication (API→Web callbacks).
var internalApiKey = builder.AddParameter("InternalApiKey", secret: true);

// SMTP credentials — optional, defaults to empty (no-op email sending when unconfigured).
var smtpUsername = builder.AddParameter("smtp-username", "", secret: true);
var smtpPassword = builder.AddParameter("smtp-password", "", secret: true);

var apiService = builder.AddProject<Projects.AspireWebAppTemplate_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("INTERNAL_API_KEY", internalApiKey)
    .WithEnvironment("Smtp__Username", smtpUsername)
    .WithEnvironment("Smtp__Password", smtpPassword);

var webfrontend = builder.AddProject<Projects.AspireWebAppTemplate_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("INTERNAL_API_KEY", internalApiKey);

// Enable API→Web service discovery for internal notification callbacks.
apiService.WithReference(webfrontend);

builder.Build().Run();
