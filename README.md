# ByHofman.Config.KeyVaultResolver

A .NET Standard 2.1 library that resolves Azure Key Vault references in `IConfigurationBuilder`. It scans configuration values for `@Microsoft.KeyVault(...)` reference strings and replaces them with the actual secrets fetched from Azure Key Vault.

## Repository structure

```
src/
  ByHofman.Config.KeyVaultResolver/        # Library source
tests/
  ByHofman.Config.KeyVaultResolver.Tests/  # xUnit test suite
docs/
  Usage.md                                 # Integration guide
```

## Installation

The package is published to GitHub Packages. Add the `byhofman` NuGet source once, then install:

```
dotnet nuget add source "https://nuget.pkg.github.com/byhofman/index.json" \
  --name byhofman --username <github-username> --password <github-token>

dotnet add package ByHofman.Config.KeyVaultResolver
```

> GitHub Packages requires authentication even for public packages. Use a personal access token with the `read:packages` scope.

## Quick start

```csharp
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
await builder.Configuration.ProcessKeyVaultReferencesAsync(credential);
```

See [docs/Usage.md](docs/Usage.md) for full integration examples and configuration reference formats.

## Requirements

- .NET Standard 2.1 compatible runtime (.NET Core 3.0+, .NET 5+)
- Azure Key Vault with secrets accessible by the supplied `TokenCredential`
