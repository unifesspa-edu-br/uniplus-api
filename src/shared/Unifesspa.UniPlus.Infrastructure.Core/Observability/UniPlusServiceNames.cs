namespace Unifesspa.UniPlus.Infrastructure.Core.Observability;

/// <summary>
/// Catálogo canônico dos nomes de serviço Uni+ — <em>single source of truth</em>
/// consumido por <c>Program.cs</c> de cada API ao chamar
/// <see cref="OpenTelemetryConfiguration.AdicionarObservabilidade"/> e
/// <see cref="Logging.SerilogConfiguration.ConfigurarSerilog(Serilog.LoggerConfiguration, Microsoft.Extensions.Configuration.IConfiguration, string?)"/>.
/// </summary>
/// <remarks>
/// <para>Centralizar os identificadores aqui garante por construção que o
/// <c>ServiceName</c> propagado pelo <see cref="Logging.ServiceNameEnricher"/>
/// (Serilog property) coincida com <c>service.name</c> do <c>Resource</c>
/// OpenTelemetry — esta é a invariante exigida pela ADR-0052 para evitar drift
/// entre logs (Loki/Console) e traces (Tempo).</para>
/// <para>Novos módulos da plataforma adicionam uma constante aqui antes de
/// instanciar a sua <c>Program.cs</c>. Em testes, <see cref="TestPlaceholder"/>
/// cobre o cenário em que <see cref="IntegrationTests.Fixtures.Hosting.ApiFactoryBase{T}"/>
/// (ou equivalentes) precisa de um nome estável que não colida com produção.</para>
/// </remarks>
public static class UniPlusServiceNames
{
    /// <summary>Módulo Seleção — API que orquestra editais, inscrições e classificação.</summary>
    public const string Selecao = "uniplus-selecao";

    /// <summary>Módulo Ingresso — API que orquestra chamadas de vagas, convocações e matrículas.</summary>
    public const string Ingresso = "uniplus-ingresso";

    /// <summary>Portal do candidato — API que orquestra perfil único e portfólio cross-módulo.</summary>
    public const string Portal = "uniplus-portal";

    /// <summary>
    /// Placeholder estável para suites de teste que sobem <c>Program.cs</c> via
    /// <c>WebApplicationFactory</c>. Permite que <see cref="Logging.ServiceNameEnricher"/>
    /// emita um valor não nulo nos logs do host de teste sem colidir com nomes de produção.
    /// </summary>
    public const string TestPlaceholder = "uniplus-test";
}
