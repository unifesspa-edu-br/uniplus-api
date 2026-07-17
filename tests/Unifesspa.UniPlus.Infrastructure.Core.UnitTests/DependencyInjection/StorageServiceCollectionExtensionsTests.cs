namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using Minio;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;

public sealed class StorageServiceCollectionExtensionsTests
{
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
        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(CompleteConfig()), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        storage.Should().BeOfType<MinioStorageService>();
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

    /// <summary>
    /// Achado de revisão (smoke test manual via Newman, Story #554): quando
    /// <c>Storage:Endpoint</c> ≠ <c>Storage:PublicEndpoint</c>, <see cref="MinioStorageService"/>
    /// tinha DOIS parâmetros de construtor do tipo <see cref="IMinioClient"/> — um "ambient"
    /// (sem chave, resolvido pelo registro <c>AddSingleton</c> não-keyed) e um explicitamente
    /// keyed via <c>[FromKeyedServices]</c>. O container de DI injetava a instância KEYED nos
    /// DOIS parâmetros — reproduzido de forma determinística contra o stack real (upload de
    /// documento do Edital falhava com <c>Connection refused</c> porque o cliente "interno"
    /// acabava usando o endpoint público). Registrar os dois clientes como keyed (nenhum
    /// consumidor injeta mais um <see cref="IMinioClient"/> "sem chave") elimina a ambiguidade.
    /// Este teste prova que os dois clientes resolvidos por chave são instâncias DISTINTAS,
    /// cada uma com o endpoint correto — a regressão apareceria como as duas instâncias
    /// colapsando na mesma (<c>BeSameAs</c>) ou com o endpoint interno errado.
    /// </summary>
    [Fact]
    public void AddUniPlusStorage_EndpointDiferenteDePublicEndpoint_ClientesInternoEPublicoSaoDistintosComEndpointCorreto()
    {
        Dictionary<string, string?> values = new(CompleteConfig())
        {
            ["Storage:PublicEndpoint"] = "localhost:9000",
        };

        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(values), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        IMinioClient interno = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);
        IMinioClient publico = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StoragePublicClientKey);

        publico.Should().NotBeSameAs(interno,
            "Endpoint (minio:9000) e PublicEndpoint (localhost:9000) divergem — devem ser instâncias " +
            "de MinioClient distintas, cada uma assinando com o próprio host");
        interno.Config.BaseUrl.Should().Be("minio:9000",
            "o cliente interno precisa continuar usando Storage:Endpoint mesmo quando o público existe");
        publico.Config.BaseUrl.Should().Be("localhost:9000");
    }

    /// <summary>
    /// Regressão direta do bug: resolve <see cref="MinioStorageService"/> por inteiro (o
    /// consumidor real com os dois parâmetros <see cref="IMinioClient"/> no construtor) e prova
    /// que cada campo interno aponta para o cliente com o endpoint certo — não apenas que os
    /// dois registros keyed, isolados, estão corretos.
    /// </summary>
    [Fact]
    public void AddUniPlusStorage_MinioStorageService_RecebeClienteInternoEPublicoCorretos()
    {
        Dictionary<string, string?> values = new(CompleteConfig())
        {
            ["Storage:PublicEndpoint"] = "localhost:9000",
            ["Storage:BucketName"] = "uniplus-documentos",
        };

        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(values), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        MinioStorageService storage = (MinioStorageService)scope.ServiceProvider.GetRequiredService<IStorageService>();

        IMinioClient internoEsperado = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);
        IMinioClient publicoEsperado = sp.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StoragePublicClientKey);

        typeof(MinioStorageService).GetField("_minioClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(storage).Should().BeSameAs(internoEsperado);
        typeof(MinioStorageService).GetField("_presignClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(storage).Should().BeSameAs(publicoEsperado);
    }

    [Fact]
    public void AddUniPlusStorage_IStorageService_DeveSerScopedDistintoEntreScopes()
    {
        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(CompleteConfig()), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        IStorageService a, b;
        using (IServiceScope scope1 = sp.CreateScope())
        {
            a = scope1.ServiceProvider.GetRequiredService<IStorageService>();
        }

        using (IServiceScope scope2 = sp.CreateScope())
        {
            b = scope2.ServiceProvider.GetRequiredService<IStorageService>();
        }

        b.Should().NotBeSameAs(a);
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
        Dictionary<string, string?> values = new()
        {
            ["Storage:Endpoint"] = "storage.example:9000",
            ["Storage:AccessKey"] = "ak",
            ["Storage:SecretKey"] = "sk",
            ["Storage:UseSSL"] = "true",
            ["Storage:Region"] = "us-east-1",
            ["Storage:BucketName"] = "uniplus-documentos",
        };

        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(values), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

        opts.Endpoint.Should().Be("storage.example:9000");
        opts.AccessKey.Should().Be("ak");
        opts.SecretKey.Should().Be("sk");
        opts.UseSSL.Should().BeTrue();
        opts.Region.Should().Be("us-east-1");
        opts.BucketName.Should().Be("uniplus-documentos");
    }
}
