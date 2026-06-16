# Personal Deployment

## Build

```bash
dotnet publish AspireWebAppTemplate.Web -c Release -o ./publish
```

## Option 1: Azure App Service

### Setup

1. Create an Azure App Service (Linux or Windows, B1 tier or free)
2. Create an Azure SQL Database (Basic tier for low traffic)
3. Configure connection string in App Service Configuration

### Deploy via GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Deploy to Azure
on:
  push:
    branches: [main]
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet publish AspireWebAppTemplate.Web -c Release -o ./publish
      - uses: azure/webapps-deploy@v3
        with:
          app-name: 'your-app-name'
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
          package: ./publish
```

## Option 2: Docker

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish AspireWebAppTemplate.Web -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AspireWebAppTemplate.Web.dll"]
```

### Run locally

```bash
docker build -t blazor-template .
docker run -p 8080:8080 -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal;..." blazor-template
```

## Option 3: Self-Hosted (Linux VPS)

1. Install .NET 10.0 runtime on the server
2. Copy published output to server
3. Set up as systemd service or run behind nginx reverse proxy
4. Configure HTTPS with Let's Encrypt / Certbot

## Environment Variables

For any deployment, configure via environment variables (not appsettings):

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=...;Database=...;
```

## Database Migration

```bash
dotnet ef database update --project AspireWebAppTemplate.ApiService --startup-project AspireWebAppTemplate.ApiService
```

Or add automatic migration in `Program.cs` for simple deployments:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}
```
