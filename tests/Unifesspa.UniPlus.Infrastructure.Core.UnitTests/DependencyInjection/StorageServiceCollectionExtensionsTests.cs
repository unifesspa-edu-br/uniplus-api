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
    public void AddUniPlusStorage_ConfigCompleta_ResolveIMinioClient()
    {
        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(CompleteConfig()), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        IMinioClient client = sp.GetRequiredService<IMinioClient>();

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
    public void AddUniPlusStorage_IMinioClient_DeveSerSingleton()
    {
        ServiceCollection services = new();
        services.AddUniPlusStorage(BuildConfig(CompleteConfig()), Env(Environments.Production));

        using ServiceProvider sp = services.BuildServiceProvider();
        IMinioClient first = sp.GetRequiredService<IMinioClient>();
        IMinioClient second = sp.GetRequiredService<IMinioClient>();

        second.Should().BeSameAs(first);
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
