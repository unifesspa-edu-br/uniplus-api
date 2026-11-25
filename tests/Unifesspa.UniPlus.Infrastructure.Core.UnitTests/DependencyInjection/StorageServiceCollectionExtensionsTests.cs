namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel.Args;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class StorageServiceCollectionExtensionsTests
{
    private static readonly TimeProvider Relogio = new RelogioFixo(new DateTimeOffset(2026, 11, 1, 12, 30, 0, TimeSpan.Zero));

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static HostingEnvironment Env(string name) =>
        new() { EnvironmentName = name };

    private static Dictionary<string, string?> CompleteConfig() => new()
    {
        ["Storage:Endpoint"] = "minio:9000",
        ["Storage:AccessKey"] = "ak-test",
        ["Storage:SecretKey"] = "sk-test",
    };

    private static ServiceProvider Provedor(Dictionary<string, string?> values)
    {
        ServiceCollection services = new();
        services.AddSingleton(Relogio);
        services.AddUniPlusStorage(BuildConfig(values), Env(Environments.Production));

        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ConfigComTodasAsPropriedades() => new()
    {
        ["Storage:Endpoint"] = "storage.example:9000",
        ["Storage:AccessKey"] = "ak",
        ["Storage:SecretKey"] = "sk",
        ["Storage:UseSSL"] = "true",
        ["Storage:Region"] = "us-east-1",
        ["Storage:BucketName"] = "uniplus-documentos",
    };

    private static Dictionary<string, string?> ConfigComEnderecoPublico() => new(CompleteConfig())
    {
        ["Storage:PublicEndpoint"] = "localhost:9000",
    };

    private static IStorageService ArmazenamentoDeUmEscopo(ServiceProvider sp)
    {
        using IServiceScope escopo = sp.CreateScope();

        return escopo.ServiceProvider.GetRequiredService<IStorageService>();
    }

    [Fact]
    public void AddUniPlusStorage_ServicesNulo_LancaArgumentNullException()
    {
        IServiceCollection? services = null;

        Action acao = () => services!.AddUniPlusStorage(BuildConfig(CompleteConfig()), Env(Environments.Production));

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusStorage_ConfigurationNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();

        Action acao = () => services.AddUniPlusStorage(null!, Env(Environments.Production));

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusStorage_EnvironmentNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();

        Action acao = () => services.AddUniPlusStorage(BuildConfig(CompleteConfig()), null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusStorage_ConfigCompleta_ResolveIMinioClientInterno()
    {
        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(CompleteConfig()), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        IMinioClient client = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);

        client.Should().NotBeNull();
    }

    [Fact]
    public void AddUniPlusStorage_ConfigCompleta_ResolveIStorageServiceComoMinioStorageService()
    {
        using ServiceProvider sp = Provedor(CompleteConfig());

        ArmazenamentoDeUmEscopo(sp).Should().BeOfType<MinioStorageService>();
    }

    [Fact]
    public void AddUniPlusStorage_IMinioClientInterno_DeveSerSingleton()
    {
        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(CompleteConfig()), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        IMinioClient first = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);
        IMinioClient second = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddUniPlusStorage_EndpointDiferenteDePublicEndpoint_ClientesInternoEPublicoSaoDistintosComEndpointCorreto()
    {
        using ServiceProvider sp = Provedor(ConfigComEnderecoPublico());
        IMinioClient interno = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);
        IMinioClient publico = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StoragePublicClientKey);

        publico.Should().NotBeSameAs(interno,
            "Endpoint (minio:9000) e PublicEndpoint (localhost:9000) divergem — devem ser instâncias " +
            "de MinioClient distintas, cada uma assinando com o próprio host");
        interno.Config.BaseUrl.Should().Be("minio:9000",
            "o cliente interno precisa continuar usando Storage:Endpoint mesmo quando o público existe");
        publico.Config.BaseUrl.Should().Be("localhost:9000");
    }

    [Fact]
    public async Task AddUniPlusStorage_ClientesDistintos_UsaCadaClienteNaOperacaoCorreta()
    {
        IMinioClient interno = Substitute.For<IMinioClient>();
        IMinioClient publico = Substitute.For<IMinioClient>();
        using ServiceProvider sp = ProvedorComClientes(interno, publico);
        using IServiceScope escopo = sp.CreateScope();
        IStorageService storage = escopo.ServiceProvider.GetRequiredService<IStorageService>();

        await storage.RemoverAsync("bucket", "objeto");
        await storage.GerarUrlTemporariaAsync("bucket", "objeto", TimeSpan.FromMinutes(5));

        await interno.Received(1).RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>());
        await publico.DidNotReceive().RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>());
        await publico.Received(1).PresignedGetObjectAsync(Arg.Any<PresignedGetObjectArgs>());
        await interno.DidNotReceive().PresignedGetObjectAsync(Arg.Any<PresignedGetObjectArgs>());
    }

    [Fact]
    public async Task AddUniPlusStorage_SemEnderecoPublico_UsaClienteInternoNasDuasOperacoes()
    {
        IMinioClient interno = Substitute.For<IMinioClient>();
        using ServiceProvider sp = ProvedorComClientes(interno);
        using IServiceScope escopo = sp.CreateScope();
        IStorageService storage = escopo.ServiceProvider.GetRequiredService<IStorageService>();

        await storage.RemoverAsync("bucket", "objeto");
        await storage.GerarUrlTemporariaAsync("bucket", "objeto", TimeSpan.FromMinutes(5));

        sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StoragePublicClientKey).Should().BeSameAs(interno);
        await interno.Received(1).RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>());
        await interno.Received(1).PresignedGetObjectAsync(Arg.Any<PresignedGetObjectArgs>());
    }

    private static ServiceProvider ProvedorComClientes(IMinioClient interno, IMinioClient? publico = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(Relogio);
        services.AddUniPlusStorage(BuildConfig(publico is null ? CompleteConfig() : ConfigComEnderecoPublico()), Env(Environments.Production));
        services.AddKeyedSingleton(StorageServiceCollectionExtensions.StorageInternalClientKey, interno);
        if (publico is not null)
        {
            services.AddKeyedSingleton(StorageServiceCollectionExtensions.StoragePublicClientKey, publico);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddUniPlusStorage_IStorageService_DeveSerScopedDistintoEntreScopes()
    {
        using ServiceProvider sp = Provedor(CompleteConfig());

        ArmazenamentoDeUmEscopo(sp).Should().NotBeSameAs(ArmazenamentoDeUmEscopo(sp));
    }

    [Theory]
    [InlineData("Storage:Endpoint")]
    [InlineData("Storage:AccessKey")]
    [InlineData("Storage:SecretKey")]
    public void AddUniPlusStorage_ProductionComCampoFaltando_OptionsValueLancaOptionsValidationException(string keyToOmit)
    {
        Dictionary<string, string?> values = new(CompleteConfig());
        values[keyToOmit] = "";

        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(values), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();

        Action acao = () => { _ = sp.GetRequiredService<IOptions<StorageOptions>>().Value; };

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*Storage:Endpoint*Storage:AccessKey*Storage:SecretKey*");
    }

    [Fact]
    public void AddUniPlusStorage_DevelopmentSemConfig_NaoLancaValidacao()
    {
        // Em Development a validação é leniente — bring-up parcial sem MinIO local
        // permite rodar API de auth/health sem subir storage. Em prod, env vars
        // do chart (Storage__Endpoint/AccessKey/SecretKey via Vault) atendem.
        ServiceCollection services = new();
        services.AddUniPlusStorage(
            BuildConfig(new Dictionary<string, string?>()),
            Env(Environments.Development));

        using ServiceProvider sp = services.BuildServiceProvider();

        Action acao = () => { _ = sp.GetRequiredService<IOptions<StorageOptions>>().Value; };

        acao.Should().NotThrow();
    }

    [Theory]
    [InlineData("http://minio:9000")]
    [InlineData("https://minio:9000")]
    [InlineData("HTTPS://minio:9000")]
    public void AddUniPlusStorage_EndpointComScheme_OptionsValueLancaOptionsValidationException(string endpoint)
    {
        Dictionary<string, string?> values = new(CompleteConfig())
        {
            ["Storage:Endpoint"] = endpoint,
        };

        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(values), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();

        Action acao = () => { _ = sp.GetRequiredService<IOptions<StorageOptions>>().Value; };

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*host:port without scheme*");
    }

    [Theory]
    [InlineData("http://storage.example.org")]
    [InlineData("https://storage.example.org")]
    [InlineData("HTTPS://storage.example.org")]
    public void AddUniPlusStorage_PublicEndpointComScheme_OptionsValueLancaOptionsValidationException(string publicEndpoint)
    {
        Dictionary<string, string?> values = new(CompleteConfig())
        {
            ["Storage:PublicEndpoint"] = publicEndpoint,
        };

        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(values), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();

        Action acao = () => { _ = sp.GetRequiredService<IOptions<StorageOptions>>().Value; };

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*Storage:PublicEndpoint*host:port without scheme*");
    }

    [Fact]
    public void AddUniPlusStorage_BindingMapeiaTodasAsPropriedades()
    {
        using ServiceProvider sp = Provedor(ConfigComTodasAsPropriedades());

        StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

        opts.Endpoint.Should().Be("storage.example:9000");
        opts.AccessKey.Should().Be("ak");
        opts.SecretKey.Should().Be("sk");
        opts.UseSSL.Should().BeTrue();
        opts.Region.Should().Be("us-east-1");
        opts.BucketName.Should().Be("uniplus-documentos");
    }
}
