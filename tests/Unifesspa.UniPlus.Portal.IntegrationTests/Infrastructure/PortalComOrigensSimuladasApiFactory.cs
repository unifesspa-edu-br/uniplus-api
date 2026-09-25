namespace Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Portal.API;
using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Sobe a Portal com as origens configuradas e as duas portas de consulta trocadas por respostas
/// controladas pelo teste. A rede das origens é coberta pelos testes dos clientes; aqui o que se
/// exercita é o endpoint inteiro — rota, composição, mapeamento de erro, cabeçalhos e corpo.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige fixture pública.")]
public sealed class PortalComOrigensSimuladasApiFactory : ApiFactoryBase<PortalApiAssemblyMarker>
{
    private const string FakeConnectionString =
        "Host=integration-not-real;Database=portal;Username=u;Password=p";

    /// <summary>Segundos configurados para o <c>Retry-After</c> da resposta 503.</summary>
    public const int ReencaminharAposEmSegundos = 42;

    public ContratoDoCertameSimulado Contrato { get; } = new();

    public RegistroDeAtosSimulado Registro { get; } = new();

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:PortalDb", FakeConnectionString),
        new("Kafka:BootstrapServers", string.Empty),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
        new("Origens:SelecaoBaseUrl", "http://uniplus-api.integration-not-real:8080"),
        new("Origens:PublicacoesBaseUrl", "http://uniplus-api.integration-not-real:8080"),
        new(
            "Origens:ReencaminharAposEmSegundos",
            ReencaminharAposEmSegundos.ToString(System.Globalization.CultureInfo.InvariantCulture)),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IContratoPublicoDoCertame>();
            services.RemoveAll<IRegistroDeAtosDoCertame>();
            services.AddSingleton<IContratoPublicoDoCertame>(Contrato);
            services.AddSingleton<IRegistroDeAtosDoCertame>(Registro);
        });
    }
}

/// <summary>Contrato público de Seleção com a resposta decidida pelo teste.</summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Exposto pela fixture pública.")]
public sealed class ContratoDoCertameSimulado : IContratoPublicoDoCertame
{
    public ConsultaDoCertame Resposta { get; set; } = new ConsultaDoCertame.NaoEncontrado();

    /// <summary>Último identificador legível pedido.</summary>
    public string? UltimoIdentificador { get; private set; }

    public Task<ConsultaDoCertame> ObterPorIdAsync(Guid processoSeletivoId, CancellationToken cancellationToken) =>
        Task.FromResult(Resposta);

    public Task<ConsultaDoCertame> ObterPorIdentificadorAsync(string identificador, CancellationToken cancellationToken)
    {
        UltimoIdentificador = identificador;
        return Task.FromResult(Resposta);
    }
}

/// <summary>Consulta de atos de Publicações com a resposta decidida pelo teste.</summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Exposto pela fixture pública.")]
public sealed class RegistroDeAtosSimulado : IRegistroDeAtosDoCertame
{
    public LeituraDosAtos Resposta { get; set; } = new([], Falha: null);

    public Task<LeituraDosAtos> LerAtosAsync(Guid processoSeletivoId, CancellationToken cancellationToken) =>
        Task.FromResult(Resposta);
}
