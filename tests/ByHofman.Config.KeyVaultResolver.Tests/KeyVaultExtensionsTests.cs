using System;
using System.Threading;
using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ByHofman.Config.KeyVaultResolver.Tests;

public class ParseReferenceTests
{
    [Fact]
    public void SecretUri_WithVersion_ParsesCorrectly()
    {
        var (vaultUri, name, version) = KeyVaultExtensions.ParseReference(
            "SecretUri=https://myvault.vault.azure.net/secrets/mysecret/abc123");

        vaultUri.Should().Be(new Uri("https://myvault.vault.azure.net/"));
        name.Should().Be("mysecret");
        version.Should().Be("abc123");
    }

    [Fact]
    public void SecretUri_WithoutVersion_ReturnsNullVersion()
    {
        var (vaultUri, name, version) = KeyVaultExtensions.ParseReference(
            "SecretUri=https://myvault.vault.azure.net/secrets/mysecret");

        vaultUri.Should().Be(new Uri("https://myvault.vault.azure.net/"));
        name.Should().Be("mysecret");
        version.Should().BeNull();
    }

    [Fact]
    public void VaultName_WithVersion_ParsesCorrectly()
    {
        var (vaultUri, name, version) = KeyVaultExtensions.ParseReference(
            "VaultName=myvault;SecretName=mysecret;SecretVersion=abc123");

        vaultUri.Should().Be(new Uri("https://myvault.vault.azure.net/"));
        name.Should().Be("mysecret");
        version.Should().Be("abc123");
    }

    [Fact]
    public void VaultName_WithoutVersion_ReturnsNullVersion()
    {
        var (vaultUri, name, version) = KeyVaultExtensions.ParseReference(
            "VaultName=myvault;SecretName=mysecret");

        vaultUri.Should().Be(new Uri("https://myvault.vault.azure.net/"));
        name.Should().Be("mysecret");
        version.Should().BeNull();
    }

    [Fact]
    public void UnrecognizedFormat_ThrowsInvalidOperationException()
    {
        var act = () => KeyVaultExtensions.ParseReference("SomeOtherFormat=value");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unrecognized Key Vault reference format*");
    }
}

public class ProcessKeyVaultReferencesTests
{
    private static Response<KeyVaultSecret> CreateSecretResponse(string name, string value)
    {
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties(name), value);
        return Response.FromValue(secret, new Mock<Response>().Object);
    }

    private static Mock<SecretClient> CreateClientMock(string secretName, string? secretVersion, string returnValue)
    {
        var mock = new Mock<SecretClient>();
        mock.Setup(c => c.GetSecretAsync(secretName, secretVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSecretResponse(secretName, returnValue));
        return mock;
    }

    [Fact]
    public async Task SecretUri_Reference_IsResolved()
    {
        var clientMock = CreateClientMock("mysecret", "abc123", "resolved-value");

        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyKey"] = "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/mysecret/abc123)"
            });

        await builder.ProcessKeyVaultReferencesAsync(
            (_, _) => clientMock.Object,
            () => Mock.Of<TokenCredential>());

        builder.Build()["MyKey"].Should().Be("resolved-value");
    }

    [Fact]
    public async Task VaultName_Reference_IsResolved()
    {
        var clientMock = CreateClientMock("mysecret", null, "resolved-value");

        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyKey"] = "@Microsoft.KeyVault(VaultName=myvault;SecretName=mysecret)"
            });

        await builder.ProcessKeyVaultReferencesAsync(
            (_, _) => clientMock.Object,
            () => Mock.Of<TokenCredential>());

        builder.Build()["MyKey"].Should().Be("resolved-value");
    }

    [Fact]
    public async Task NonKeyVaultValues_AreLeftUnchanged()
    {
        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyKey"] = "plain-value"
            });

        await builder.ProcessKeyVaultReferencesAsync(
            (_, _) => throw new Exception("should not be called"),
            () => throw new Exception("should not be called"));

        builder.Build()["MyKey"].Should().Be("plain-value");
    }

    [Fact]
    public async Task MultipleReferences_AllResolved()
    {
        var mock = new Mock<SecretClient>();
        mock.Setup(c => c.GetSecretAsync("secret-a", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSecretResponse("secret-a", "value-a"));
        mock.Setup(c => c.GetSecretAsync("secret-b", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSecretResponse("secret-b", "value-b"));

        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KeyA"] = "@Microsoft.KeyVault(VaultName=vault;SecretName=secret-a)",
                ["KeyB"] = "@Microsoft.KeyVault(VaultName=vault;SecretName=secret-b)"
            });

        await builder.ProcessKeyVaultReferencesAsync(
            (_, _) => mock.Object,
            () => Mock.Of<TokenCredential>());

        var config = builder.Build();
        config["KeyA"].Should().Be("value-a");
        config["KeyB"].Should().Be("value-b");
    }
}
