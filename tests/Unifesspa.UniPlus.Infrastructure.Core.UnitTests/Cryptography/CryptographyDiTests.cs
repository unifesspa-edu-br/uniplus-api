namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class CryptographyDiTests
{
    private static IConfiguration CriarConfig(
        string provider,
        string? localKey = null,
        string? vaultAddress = null,
        string? kubernetesRole = null,
        string? vaultToken = null)
    {
        Dictionary<string, string?> values = new()
        {
            ["UniPlus:Encryption:Provider"] = provider,
        };

        if (localKey is not null)
        {
            values["UniPlus:Encryption:LocalKey"] = localKey;
        }

        if (vaultAddress is not null)
        {
            values["UniPlus:Encryption:VaultAddress"] = vaultAddress;
        }

        if (kubernetesRole is not null)
        {
            values["UniPlus:Encryption:KubernetesRole"] = kubernetesRole;
        }

        if (vaultToken is not null)
        {
            values["UniPlus:Encryption:VaultToken"] = vaultToken;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderLocal_DeveResolverLocalAesEncryptionService()
    {
        string chaveValida = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("local", chaveValida))
            .BuildServiceProvider();

        IUniPlusEncryptionService servico = sp.GetRequiredService<IUniPlusEncryptionService>();

        servico.Should().BeOfType<LocalAesEncryptionService>();
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderInvalido_DeveLancarOptionsValidationExceptionAoResolver()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("desconhecido"))
            .BuildServiceProvider();

        Action ato = () => sp.GetRequiredService<IUniPlusEncryptionService>();

        ato.Should().Throw<OptionsValidationException>()
            .WithMessage("*'desconhecido'*");
    }

    // ─── Fail-fast no boot (IStartupValidator) ────────────────────────────────

    [Fact]
    public void AddUniPlusEncryption_ProviderLocalSemLocalKey_DeveLancarOptionsValidationExceptionNoStart()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("local"))
            .BuildServiceProvider();

        IStartupValidator startupValidator = sp.GetRequiredService<IStartupValidator>();

        Action ato = () => startupValidator.Validate();

        ato.Should().Throw<OptionsValidationException>()
            .WithMessage("*UniPlus:Encryption:LocalKey*");
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderVaultSemVaultAddress_DeveLancarOptionsValidationExceptionNoStart()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("vault", kubernetesRole: "uniplus-api"))
            .BuildServiceProvider();

        IStartupValidator startupValidator = sp.GetRequiredService<IStartupValidator>();

        Action ato = () => startupValidator.Validate();

        ato.Should().Throw<OptionsValidationException>()
            .WithMessage("*UniPlus:Encryption:VaultAddress*");
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderLocalComLocalKeyValida_NaoLancaNoStart()
    {
        string chaveValida = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("local", chaveValida))
            .BuildServiceProvider();

        IStartupValidator startupValidator = sp.GetRequiredService<IStartupValidator>();

        Action ato = () => startupValidator.Validate();

        ato.Should().NotThrow();
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderVaultComKubernetesRole_NaoLancaNoStart()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig(
                "vault",
                vaultAddress: "http://platform-vault-uniplus-standalone.vault.svc.cluster.local:8200",
                kubernetesRole: "uniplus-api"))
            .BuildServiceProvider();

        IStartupValidator startupValidator = sp.GetRequiredService<IStartupValidator>();

        Action ato = () => startupValidator.Validate();

        ato.Should().NotThrow();
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderLocalLocalKeyBase64Invalida_DeveLancarOptionsValidationExceptionNoStart()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("local", localKey: "não-é-base64!"))
            .BuildServiceProvider();

        IStartupValidator startupValidator = sp.GetRequiredService<IStartupValidator>();

        Action ato = () => startupValidator.Validate();

        ato.Should().Throw<OptionsValidationException>()
            .WithMessage("*Base64*");
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderLocalLocalKeyTamanhoErrado_DeveLancarOptionsValidationExceptionNoStart()
    {
        string chave16Bytes = Convert.ToBase64String(new byte[16]);
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("local", localKey: chave16Bytes))
            .BuildServiceProvider();

        IStartupValidator startupValidator = sp.GetRequiredService<IStartupValidator>();

        Action ato = () => startupValidator.Validate();

        ato.Should().Throw<OptionsValidationException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderVaultSemAuthMethod_DeveLancarOptionsValidationExceptionNoStart()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig(
                "vault",
                vaultAddress: "http://platform-vault-uniplus-standalone.vault.svc.cluster.local:8200"))
            .BuildServiceProvider();

        IStartupValidator startupValidator = sp.GetRequiredService<IStartupValidator>();

        Action ato = () => startupValidator.Validate();

        ato.Should().Throw<OptionsValidationException>()
            .WithMessage("*KubernetesRole*");
    }

    // ─── Determinismo do auth method em VaultTransitEncryptionService ─────────

    [Fact]
    public void AddUniPlusEncryption_ProviderVaultKubernetesRoleSemJwtNoDisco_DeveLancarInvalidOperationExceptionAoResolver()
    {
        // Config válida do ponto de vista do validator (KubernetesRole sozinho),
        // mas o construtor do VaultTransitEncryptionService falha porque o JWT
        // não existe no path configurado.
        string pathInexistente = Path.Combine(Path.GetTempPath(), $"uniplus-test-no-jwt-{Guid.NewGuid():N}");
        Dictionary<string, string?> values = new()
        {
            ["UniPlus:Encryption:Provider"] = "vault",
            ["UniPlus:Encryption:VaultAddress"] = "http://vault.vault.svc:8200",
            ["UniPlus:Encryption:KubernetesRole"] = "uniplus-api",
            ["UniPlus:Encryption:KubernetesJwtPath"] = pathInexistente,
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(config)
            .BuildServiceProvider();

        Action ato = () => sp.GetRequiredService<IUniPlusEncryptionService>();

        ato.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains(pathInexistente)
                && e.Message.Contains("ServiceAccount"));
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderVaultVaultTokenSemJwt_DeveResolverServicoSemTocarOJwt()
    {
        // Caminho dev/CI: VaultToken estático, sem JWT do K8s. O construtor não deve
        // tentar ler disco quando a config seleciona token auth.
        Dictionary<string, string?> values = new()
        {
            ["UniPlus:Encryption:Provider"] = "vault",
            ["UniPlus:Encryption:VaultAddress"] = "http://vault.vault.svc:8200",
            ["UniPlus:Encryption:VaultToken"] = "hvs.dev",
            ["UniPlus:Encryption:KubernetesJwtPath"] = "/path/inexistente",
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(config)
            .BuildServiceProvider();

        Action ato = () => sp.GetRequiredService<IUniPlusEncryptionService>();

        ato.Should().NotThrow();
    }
}
