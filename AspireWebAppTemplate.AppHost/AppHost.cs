var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AspireWebAppTemplate_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.AspireWebAppTemplate_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
