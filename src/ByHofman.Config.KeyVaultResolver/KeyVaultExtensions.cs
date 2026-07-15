using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ByHofman.Config.KeyVaultResolver;

public static class KeyVaultExtensions
{
    private static readonly Regex KeyVaultRegex =
        new Regex(@"@Microsoft\.KeyVault\(([^\)]+)\)");

    public static Task<IConfigurationBuilder> ProcessKeyVaultReferencesAsync(
        this IConfigurationBuilder builder,
        TokenCredential tokenCredential,
        ILogger? logger = null)
        => builder.ProcessKeyVaultReferencesAsync(
            (uri, cred) => new SecretClient(uri, cred),
            () => tokenCredential,
            logger);

    internal static async Task<IConfigurationBuilder> ProcessKeyVaultReferencesAsync(
        this IConfigurationBuilder builder,
        Func<Uri, TokenCredential, SecretClient> clientFactory,
        Func<TokenCredential> credentialFactory,
        ILogger? logger = null)
    {
        var configuration = builder.Build();

        // Scan for references first — no credentials needed for this step
        var references = new List<(string Key, Uri VaultUri, string SecretName, string? SecretVersion)>();
        foreach (var pair in configuration.AsEnumerable())
        {
            if (pair.Value is null) continue;
            var match = KeyVaultRegex.Match(pair.Value);
            if (!match.Success) continue;
            var (vaultUri, secretName, secretVersion) = ParseReference(match.Groups[1].Value);
            references.Add((pair.Key, vaultUri, secretName, secretVersion));
        }

        if (references.Count == 0)
            return builder;

        // References found — acquire credential and resolve
        var credential = credentialFactory();
        var resolved = new Dictionary<string, string?>();
        foreach (var (key, vaultUri, secretName, secretVersion) in references)
        {
            var client = clientFactory(vaultUri, credential);
            KeyVaultSecret secret = await client.GetSecretAsync(secretName, secretVersion);
            resolved[key] = secret.Value;
            logger?.LogInformation("Successfully resolved KeyVault secret for {ConfigKey}", key);
        }

        builder.AddInMemoryCollection(resolved);
        return builder;
    }

    internal static (Uri vaultUri, string secretName, string? secretVersion) ParseReference(string content)
    {
        // Format 1: SecretUri=https://vault.vault.azure.net/secrets/name/version
        var secretUriMatch = Regex.Match(content, @"SecretUri=([^;)]+)");
        if (secretUriMatch.Success)
        {
            var uri = new Uri(secretUriMatch.Groups[1].Value.Trim());
            string name = uri.Segments[2].TrimEnd('/');
            string? version = uri.Segments.Length > 3 ? uri.Segments[3].TrimEnd('/') : null;
            return (new Uri($"{uri.Scheme}://{uri.Host}/"), name, version);
        }

        // Format 2: VaultName=myvault;SecretName=mysecret;SecretVersion=version
        var vaultNameMatch = Regex.Match(content, @"VaultName=([^;)]+)");
        var secretNameMatch = Regex.Match(content, @"SecretName=([^;)]+)");
        if (vaultNameMatch.Success && secretNameMatch.Success)
        {
            string vaultName = vaultNameMatch.Groups[1].Value.Trim();
            string secretName = secretNameMatch.Groups[1].Value.Trim();
            var versionMatch = Regex.Match(content, @"SecretVersion=([^;)]+)");
            string? secretVersion = versionMatch.Success ? versionMatch.Groups[1].Value.Trim() : null;
            return (new Uri($"https://{vaultName}.vault.azure.net/"), secretName, secretVersion);
        }

        throw new InvalidOperationException($"Unrecognized Key Vault reference format: {content}");
    }
}
