# Usage

`ByHofman.Config.KeyVaultResolver` resolves `@Microsoft.KeyVault(...)` reference strings in your configuration by fetching the actual secret values from Azure Key Vault at startup.

## Installation

Add the NuGet package to your project:

```xml
<PackageReference Include="ByHofman.Config.KeyVaultResolver" Version="*" />
```

## Basic usage

Call `ProcessKeyVaultReferencesAsync` on any `IConfigurationBuilder` before the application starts using configuration. The method scans all current configuration values, fetches any that contain a Key Vault reference, and adds the resolved values back as an in-memory configuration source.

```csharp
using Azure.Identity;
using ByHofman.Config.KeyVaultResolver;

var builder = WebApplication.CreateBuilder(args);

var credential = new ClientSecretCredential(
    builder.Configuration["AzureAd:TenantId"],
    builder.Configuration["AzureAd:ClientId"],
    builder.Configuration["AzureAd:ClientSecret"]);

await builder.Configuration.ProcessKeyVaultReferencesAsync(credential);
```

Any credential type from `Azure.Identity` is supported — `DefaultAzureCredential`, `ManagedIdentityCredential`, `WorkloadIdentityCredential`, etc.:

```csharp
// Managed identity (recommended for hosted environments)
await builder.Configuration.ProcessKeyVaultReferencesAsync(new ManagedIdentityCredential());

// Default credential chain (local dev + managed identity in production)
await builder.Configuration.ProcessKeyVaultReferencesAsync(new DefaultAzureCredential());
```

## Configuration reference formats

Two formats are supported, matching the Azure App Service Key Vault reference syntax.

### SecretUri

References a secret by its full URI, optionally pinning to a specific version:

```json
{
  "ConnectionStrings": {
    "Database": "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/db-connection/abc123)"
  }
}
```

Omit the version segment to always fetch the latest version:

```json
{
  "ConnectionStrings": {
    "Database": "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/db-connection)"
  }
}
```

### VaultName + SecretName

References a secret by vault name and secret name. The vault URL is inferred as `https://{VaultName}.vault.azure.net/`:

```json
{
  "ConnectionStrings": {
    "Database": "@Microsoft.KeyVault(VaultName=myvault;SecretName=db-connection)"
  }
}
```

Pin to a specific version using `SecretVersion`:

```json
{
  "ConnectionStrings": {
    "Database": "@Microsoft.KeyVault(VaultName=myvault;SecretName=db-connection;SecretVersion=abc123)"
  }
}
```

## Logging

Pass an `ILogger` to get a log entry for each resolved key:

```csharp
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger("KeyVaultResolver");

await builder.Configuration.ProcessKeyVaultReferencesAsync(credential, logger);
```

Output example:

```
info: KeyVaultResolver[0]
      Successfully resolved KeyVault secret for ConnectionStrings:Database
```

## Azure Key Vault access

The identity used must have the **Key Vault Secrets User** role (or the legacy `get` secrets permission) on the vault. Assign this in the Azure portal under the vault's **Access control (IAM)** blade.

## appsettings example

```json
{
  "AzureAd": {
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "ClientSecret": "<client-secret>"
  },
  "ConnectionStrings": {
    "Database": "@Microsoft.KeyVault(VaultName=myvault;SecretName=ConnectionStrings--Database)"
  },
  "ApplicationInsights": {
    "ConnectionString": "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/AppInsights--ConnectionString)"
  }
}
```
