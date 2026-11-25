namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cryptography;

using System.Security.Cryptography;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        KeyValuePair<string, string?>[] opcoes =
        [
            new("UniPlus:Encryption:Provider", provider),
            new("UniPlus:Encryption:LocalKey", localKey),
            new("UniPlus:Encryption:VaultAddress", vaultAddress),
            new("UniPlus:Encryption:KubernetesRole", kubernetesRole),
            new("UniPlus:Encryption:VaultToken", vaultToken),
        ];
        return new ConfigurationBuilder()
            .AddInMemoryCollection(opcoes.Where(opcao => opcao.Value is not null))
            .Build();
    }

    private static IConfiguration CriarConfigVaultComJwt(string path) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["UniPlus:Encryption:Provider"] = "vault",
            ["UniPlus:Encryption:VaultAddress"] = "http://vault.vault.svc:8200",
            ["UniPlus:Encryption:KubernetesRole"] = "uniplus-api",
            ["UniPlus:Encryption:KubernetesJwtPath"] = path,
        })
        .Build();

    private static IConfiguration CriarConfigVaultComToken() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["UniPlus:Encryption:Provider"] = "vault",
            ["UniPlus:Encryption:VaultAddress"] = "http://vault.vault.svc:8200",
            ["UniPlus:Encryption:VaultToken"] = "hvs.dev",
            ["UniPlus:Encryption:KubernetesJwtPath"] = "/path/inexistente",
        })
        .Build();

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

    // ─── IUniPlusBlindIndexService resolve pelo mesmo Provider ────────────────

    [Fact]
    public void AddUniPlusEncryption_ProviderLocal_DeveResolverLocalHmacBlindIndexService()
    {
        string chaveValida = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig("local", chaveValida))
            .BuildServiceProvider();

        IUniPlusBlindIndexService servico = sp.GetRequiredService<IUniPlusBlindIndexService>();

        servico.Should().BeOfType<LocalHmacBlindIndexService>();
    }

    [Fact]
    public void AddUniPlusEncryption_ProviderVaultComVaultToken_DeveResolverVaultTransitBlindIndexService()
    {
        ServiceProvider sp = new ServiceCollection()
            .AddLogging()
            .AddUniPlusEncryption(CriarConfig(
                "vault",
                vaultAddress: "http://vault.vault.svc:8200",
                vaultToken: "hvs.dev"))
            .BuildServiceProvider();

        IUniPlusBlindIndexService servico = sp.GetRequiredService<IUniPlusBlindIndexService>();

        servico.Should().BeOfType<VaultTransitBlindIndexService>();
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
        IConfiguration config = CriarConfigVaultComJwt(pathInexistente);

        ServiceProvider sp = CriarServicosDeCriptografia(config);

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
        IConfiguration config = CriarConfigVaultComToken();

        ServiceProvider sp = CriarServicosDeCriptografia(config);

        Action ato = () => sp.GetRequiredService<IUniPlusEncryptionService>();

        ato.Should().NotThrow();
    }

    // ─── Warmup hosted service (fail-fast no Host.StartAsync) ─────────────────

    private static ServiceProvider CriarServicosDeCriptografia(IConfiguration config) => new ServiceCollection()
        .AddLogging()
        .AddUniPlusEncryption(config)
        .BuildServiceProvider();

    private static IHost CriarHostComEncryption(IConfiguration config)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Services.AddLogging();
        builder.Services.AddUniPlusEncryption(config);
        return builder.Build();
    }

    [Fact]
    public async Task EncryptionWarmupHostedService_ProviderLocalSemLocalKey_StartAsync_DeveLancarOptionsValidationException()
    {
        using IHost host = CriarHostComEncryption(CriarConfig("local"));

        Func<Task> ato = () => host.StartAsync();

        await ato.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*UniPlus:Encryption:LocalKey*");
    }

    [Fact]
    public async Task EncryptionWarmupHostedService_ProviderVaultKubernetesRoleSemJwtNoDisco_StartAsync_DeveLancarInvalidOperationException()
    {
        // EncryptionOptionsValidator passa (KubernetesRole + VaultAddress válidos),
        // mas o construtor de VaultTransitEncryptionService falha em ReadJwtOrThrow
        // porque o JWT path não existe. O warmup garante falha na inicialização.
        string pathInexistente = Path.Combine(Path.GetTempPath(), $"uniplus-warmup-test-{Guid.NewGuid():N}");
        IConfiguration config = CriarConfigVaultComJwt(pathInexistente);

        using IHost host = CriarHostComEncryption(config);

        Func<Task> ato = () => host.StartAsync();

        await ato.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains(pathInexistente)
                && e.Message.Contains("ServiceAccount"));
    }

    [Fact]
    public async Task EncryptionWarmupHostedService_ProviderLocalValido_StartAsync_NaoLanca()
    {
        string chaveValida = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using IHost host = CriarHostComEncryption(CriarConfig("local", chaveValida));

        Func<Task> ato = () => host.StartAsync();

        await ato.Should().NotThrowAsync();

        await host.StopAsync();
    }
}
