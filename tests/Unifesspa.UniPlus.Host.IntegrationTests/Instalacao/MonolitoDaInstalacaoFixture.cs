namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using System.Diagnostics.CodeAnalysis;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Monólito com Postgres, RabbitMQ, MinIO e Mailpit efêmeros: o PDF de designação sobe ao MinIO real pela URL assinada,
/// o resultado do verificador volta pela fila real, e o código da instalação chega a um relay de captura.
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por EncerrarDependenciasAsync — xUnit invoca deterministicamente.")]
public sealed class MonolitoDaInstalacaoFixture : MonolitoPostgresFixtureBase<MonolitoDaInstalacaoApiFactory>
{
    public const string BucketDeEnvio = "uniplus-candidato-envio";
    public const string BucketDefinitivo = "uniplus-candidato-definitivo";

    private const ushort PortaSmtp = 1025;
    private const ushort PortaHttp = 8025;

    private readonly MinioContainerFixture _minio = new();

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.27")
        .WithPortBinding(PortaSmtp, true)
        .WithPortBinding(PortaHttp, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(PortaHttp).ForPath("/readyz")))
        .Build();

    public Uri ApiDoMailpit => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(PortaHttp)}");

    public string EnderecoDoMinio => _minio.Endpoint;

    /// <summary>Arquivo de log da API sob teste: é onde o teste confere que o código nunca foi registrado.</summary>
    public string ArquivoDeLog { get; } = Path.Join(Path.GetTempPath(), $"uniplus-instalacao-{Guid.NewGuid():N}.log");

    // O Serilog é montado antes de a fábrica de teste aplicar a configuração em memória: só variável de ambiente chega
    // a tempo de ligar o arquivo de log.
    private static readonly string[] VariaveisDoLog =
        ["Serilog__WriteTo__0__Name", "Serilog__WriteTo__0__Args__path", "Serilog__WriteTo__0__Args__shared"];

    protected override async Task IniciarDependenciasAsync()
    {
        Environment.SetEnvironmentVariable(VariaveisDoLog[0], "File");
        Environment.SetEnvironmentVariable(VariaveisDoLog[1], ArquivoDeLog);
        Environment.SetEnvironmentVariable(VariaveisDoLog[2], "true");
        await Task.WhenAll(_mailpit.StartAsync(), _minio.InitializeAsync()).ConfigureAwait(false);
    }

    private readonly SemaphoreSlim _provisionamento = new(1, 1);
    private bool _bucketsProvisionados;

    /// <summary>Cria os dois buckets uma vez só; a primeira chamada de cada suíte paga o custo.</summary>
    public async Task ProvisionarBucketsAsync()
    {
        await _provisionamento.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_bucketsProvisionados)
            {
                return;
            }

            await CriarBucketsAsync().ConfigureAwait(false);
            _bucketsProvisionados = true;
        }
        finally
        {
            _provisionamento.Release();
        }
    }

    private async Task CriarBucketsAsync()
    {
        // A credencial geral cria o bucket; a da API, que só opera sobre os objetos, nunca o cria.
        using IServiceScope scope = Factory.Services.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        foreach (string bucket in new[] { BucketDeEnvio, BucketDefinitivo })
        {
            using MemoryStream vazio = new([0]);
            await storage.UploadAsync(bucket, "provisionamento", vazio, "application/octet-stream").ConfigureAwait(false);
            await storage.RemoverAsync(bucket, "provisionamento").ConfigureAwait(false);
        }
    }

    protected override async Task EncerrarDependenciasAsync()
    {
        foreach (string variavel in VariaveisDoLog)
        {
            Environment.SetEnvironmentVariable(variavel, null);
        }

        try
        {
            await _mailpit.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _minio.DisposeAsync().ConfigureAwait(false);
            _provisionamento.Dispose();
        }
    }

    protected override MonolitoDaInstalacaoApiFactory CreateFactory(string connectionString) =>
        new(connectionString, _mailpit.Hostname, _mailpit.GetMappedPublicPort(PortaSmtp), _minio.Endpoint);
}

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative usado como estado de fixture.")]
public sealed class MonolitoDaInstalacaoApiFactory : MonolitoApiFactory
{
    /// <summary>O domínio das personas do portal, tratado como institucional nesta suíte.</summary>
    public const string DominioInstitucional = "uniplus-test.local";

    private readonly string _hostSmtp;
    private readonly int _portaSmtp;
    private readonly string _endpointDoMinio;

    public MonolitoDaInstalacaoApiFactory(
        string connectionString, string hostSmtp, int portaSmtp, string endpointDoMinio)
        : base(connectionString, wolverineEnabled: true)
    {
        _hostSmtp = hostSmtp;
        _portaSmtp = portaSmtp;
        _endpointDoMinio = endpointDoMinio;
    }

    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
    [
        new("Email:Smtp:Host", _hostSmtp),
        new("Email:Smtp:Porta", _portaSmtp.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("Email:Smtp:Seguranca", "Nenhuma"),
        new("Email:Smtp:Remetente", "nao-responda@uniplus.local"),
        new("Storage:Endpoint", _endpointDoMinio),
        new("Storage:AccessKey", MinioContainerFixture.AccessKey),
        new("Storage:SecretKey", MinioContainerFixture.SecretKey),
        new("ArquivosDoCandidato:AccessKey", MinioContainerFixture.AccessKey),
        new("ArquivosDoCandidato:SecretKey", MinioContainerFixture.SecretKey),
        new("ArquivosDoCandidato:BucketDeEnvio", MonolitoDaInstalacaoFixture.BucketDeEnvio),
        new("ArquivosDoCandidato:BucketDefinitivo", MonolitoDaInstalacaoFixture.BucketDefinitivo),
        new("Autorizacao:Instalacao:DominiosInstitucionais:0", DominioInstitucional),
    ];
}
