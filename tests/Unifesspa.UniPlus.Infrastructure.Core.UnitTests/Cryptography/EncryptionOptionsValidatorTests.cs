namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

public sealed class EncryptionOptionsValidatorTests
{
    private static readonly string ChaveBase64Valida =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static readonly EncryptionOptionsValidator Sut = new();

    // ─── Provider ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ProviderNulo_DeveFalhar()
    {
        EncryptionOptions opts = new() { Provider = null! };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle()
            .Which.Should().Contain("UniPlus:Encryption:Provider é obrigatório");
    }

    [Fact]
    public void Validate_ProviderVazio_DeveFalhar()
    {
        EncryptionOptions opts = new() { Provider = "   " };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle()
            .Which.Should().Contain("UniPlus:Encryption:Provider é obrigatório");
    }

    [Fact]
    public void Validate_ProviderDesconhecido_DeveFalharListandoValoresAceitos()
    {
        EncryptionOptions opts = new() { Provider = "aws" };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        string mensagem = resultado.Failures!.Single();
        mensagem.Should().Contain("'aws'");
        mensagem.Should().Contain("'local'");
        mensagem.Should().Contain("'vault'");
    }

    [Fact]
    public void Validate_ProviderEmCaixaAlta_DeveSerCaseInsensitive()
    {
        EncryptionOptions opts = new() { Provider = "LOCAL", LocalKey = ChaveBase64Valida };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Succeeded.Should().BeTrue();
    }

    // ─── Local ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_LocalSemLocalKey_DeveFalharMencionandoPath()
    {
        EncryptionOptions opts = new() { Provider = "local", LocalKey = null };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle()
            .Which.Should().Contain("UniPlus:Encryption:LocalKey");
    }

    [Fact]
    public void Validate_LocalLocalKeyEmBranco_DeveFalhar()
    {
        EncryptionOptions opts = new() { Provider = "local", LocalKey = "   " };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle()
            .Which.Should().Contain("UniPlus:Encryption:LocalKey");
    }

    [Fact]
    public void Validate_LocalLocalKeyBase64Invalida_DeveFalharMencionandoBase64()
    {
        EncryptionOptions opts = new() { Provider = "local", LocalKey = "não-é-base64!!!" };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle()
            .Which.Should().Contain("Base64");
    }

    [Fact]
    public void Validate_LocalLocalKeyComTamanhoErrado_DeveFalharMencionando32Bytes()
    {
        string chave16Bytes = Convert.ToBase64String(new byte[16]);
        EncryptionOptions opts = new() { Provider = "local", LocalKey = chave16Bytes };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        string mensagem = resultado.Failures!.Single();
        mensagem.Should().Contain("32 bytes");
        mensagem.Should().Contain("16 bytes");
    }

    [Fact]
    public void Validate_LocalLocalKey32BytesValida_DeveSerSuccess()
    {
        EncryptionOptions opts = new() { Provider = "local", LocalKey = ChaveBase64Valida };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Succeeded.Should().BeTrue();
    }

    // ─── Vault ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_VaultSemVaultAddress_DeveFalhar()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = null,
            KubernetesRole = "uniplus-api",
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle()
            .Which.Should().Contain("UniPlus:Encryption:VaultAddress");
    }

    [Fact]
    public void Validate_VaultComVaultAddressEKubernetesRole_DeveSerSuccess()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault.vault.svc:8200",
            KubernetesRole = "uniplus-api",
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_VaultComVaultAddressEVaultToken_DeveSerSuccess()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://127.0.0.1:8200",
            VaultToken = "hvs.test-only-token",
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_VaultSemRoleNemToken_DeveFalhar()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault.vault.svc:8200",
            KubernetesRole = null,
            VaultToken = null,
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().ContainSingle()
            .Which.Should().Contain("KubernetesRole");
    }

    [Fact]
    public void Validate_VaultComKubernetesRoleEVaultTokenSimultaneamente_DeveFalhar()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "http://vault.vault.svc:8200",
            KubernetesRole = "uniplus-api",
            VaultToken = "hvs.dev",
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        string mensagem = resultado.Failures!.Single();
        mensagem.Should().Contain("mutuamente exclusivos");
    }

    [Fact]
    public void Validate_VaultComVaultAddressMalformado_DeveFalhar()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "vault.svc:8200",   // sem scheme
            KubernetesRole = "uniplus-api",
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        string mensagem = resultado.Failures!.Single();
        mensagem.Should().Contain("UniPlus:Encryption:VaultAddress");
        mensagem.Should().Contain("URL absoluta");
    }

    [Fact]
    public void Validate_VaultComVaultAddressEsquemaFile_DeveFalhar()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = "file:///etc/vault",
            KubernetesRole = "uniplus-api",
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures!.Single().Should().Contain("http ou https");
    }

    [Fact]
    public void Validate_VaultSemAddressESemRoleNemToken_DeveAcumularFalhas()
    {
        EncryptionOptions opts = new()
        {
            Provider = "vault",
            VaultAddress = null,
            KubernetesRole = null,
            VaultToken = null,
        };

        ValidateOptionsResult resultado = Sut.Validate(Options.DefaultName, opts);

        resultado.Failed.Should().BeTrue();
        resultado.Failures.Should().HaveCount(2);
        resultado.Failures!.Should().Contain(f => f.Contains("UniPlus:Encryption:VaultAddress"));
        resultado.Failures!.Should().Contain(f => f.Contains("KubernetesRole"));
    }

    // ─── Named options ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NameDiferenteDoDefault_DeveSkipar()
    {
        EncryptionOptions opts = new() { Provider = "local", LocalKey = null };

        ValidateOptionsResult resultado = Sut.Validate(name: "outra-config", opts);

        resultado.Skipped.Should().BeTrue();
    }
}
