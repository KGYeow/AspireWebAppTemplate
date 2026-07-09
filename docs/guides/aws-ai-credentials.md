# AWS AI Credentials Guide

This guide explains how to configure AWS credentials for the AI integration (Amazon Bedrock) during local development and in production.

## Overview

The AI service uses a three-tier credential resolution strategy:

1. **Session credentials** — Access Key ID + Secret Access Key + Session Token (temporary credentials from AWS console)
2. **Basic credentials** — Access Key ID + Secret Access Key only (long-lived IAM user credentials)
3. **Default credential chain** — No explicit credentials; falls through to IAM roles (production)

In local development, you provide temporary session credentials via Aspire secret parameters. In production, no explicit credentials are needed — the application uses IAM roles automatically.

## Local Development Setup

### Step 1: Obtain AWS Credentials from the Console

1. Sign in to the AWS Management Console
2. Navigate to your SSO start page or IAM Identity Center
3. Click your account and select the appropriate role
4. Choose **Option 3: "Use individual values in your AWS service client"**
5. Copy the three values:
   - `AWS_ACCESS_KEY_ID`
   - `AWS_SECRET_ACCESS_KEY`
   - `AWS_SESSION_TOKEN`

### Step 2: First Run — Aspire Prompts for Secrets

When you run the AppHost for the first time, Aspire detects the three secret parameters defined in `AppHost.cs` and prompts you for each value:

```
Parameters:ai-access-key-id: <paste your Access Key ID>
Parameters:ai-secret-access-key: <paste your Secret Access Key>
Parameters:ai-session-token: <paste your Session Token>
```

Aspire stores these values encrypted in .NET User Secrets for the AppHost project. Subsequent runs use the cached values automatically — you won't be prompted again until you clear or update them.

### Step 3: How It Works

The AppHost defines three secret parameters that are passed to the ApiService as environment variables:

```csharp
// AppHost Program.cs
var aiAccessKeyId = builder.AddParameter("ai-access-key-id", secret: true);
var aiSecretAccessKey = builder.AddParameter("ai-secret-access-key", secret: true);
var aiSessionToken = builder.AddParameter("ai-session-token", secret: true);

var apiService = builder.AddProject<Projects.AspireWebAppTemplate_ApiService>("apiservice")
    .WithEnvironment("Ai__AccessKeyId", aiAccessKeyId)
    .WithEnvironment("Ai__SecretAccessKey", aiSecretAccessKey)
    .WithEnvironment("Ai__SessionToken", aiSessionToken);
```

The ApiService reads these via standard ASP.NET Core configuration (`Ai:AccessKeyId`, `Ai:SecretAccessKey`, `Ai:SessionToken`) and constructs the appropriate AWS credentials object.

## Refreshing Expired Session Tokens

AWS session tokens are temporary and expire (typically after 1–12 hours depending on your organization's configuration). When your token expires, you'll see an error indicating the credentials have expired.

To refresh, run the following commands from the **AppHost project directory**:

```bash
cd AspireWebAppTemplate.AppHost
```

Update all three values (copy fresh credentials from the AWS console using Option 3):

```bash
dotnet user-secrets set "Parameters:ai-access-key-id" "your-new-access-key-id"
dotnet user-secrets set "Parameters:ai-secret-access-key" "your-new-secret-access-key"
dotnet user-secrets set "Parameters:ai-session-token" "your-new-session-token"
```

If only the session token has expired (Access Key ID and Secret Access Key remain the same):

```bash
dotnet user-secrets set "Parameters:ai-session-token" "your-new-session-token"
```

Restart the AppHost after updating secrets for the changes to take effect.

## Production Deployment

In production, **do not set any credential environment variables**. Leave `Ai__AccessKeyId`, `Ai__SecretAccessKey`, and `Ai__SessionToken` unset.

When no explicit credentials are configured, the AI service falls through to the AWS SDK default credential chain. This supports:

- **IAM Task Roles** — when running on Amazon ECS
- **Instance Profiles** — when running on Amazon EC2
- **IAM Roles for Service Accounts (IRSA)** — when running on Amazon EKS

This approach is more secure than static credentials because:

- No secrets to manage, rotate, or risk leaking
- Credentials are automatically provisioned and rotated by AWS
- Access is scoped to the specific service via IAM policies

## Non-Secret Configuration

The AI model ID and AWS region are non-secret values configured in `appsettings.json` (not via User Secrets):

```json
{
  "Ai": {
    "ModelId": "us.amazon.nova-2-lite-v1:0",
    "Region": "us-east-1"
  }
}
```

These values are safe to commit to source control and can be overridden per environment via `appsettings.Production.json` or environment variables.

> **Note:** Amazon Nova 2 Lite requires a cross-region inference profile ID (`us.amazon.nova-2-lite-v1:0`) rather than the base model ID (`amazon.nova-2-lite-v1:0`). The `us.` prefix indicates the US cross-region inference profile. If you're in a different geography (EU, APAC), use the corresponding prefix (e.g., `eu.amazon.nova-2-lite-v1:0`).

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "AWS credentials have expired" | Session token expired | Refresh credentials via `dotnet user-secrets set` (see above) |
| "Region is not configured" | Missing `Ai:Region` in appsettings | Add `Ai:Region` to `appsettings.json` |
| "The configured model is unavailable" | Model ID doesn't exist in your region | Verify `Ai:ModelId` and ensure model access is enabled in your AWS account |
| Aspire prompts for secrets every run | User Secrets not persisting | Verify the AppHost project has a `UserSecretsId` in its `.csproj` |
