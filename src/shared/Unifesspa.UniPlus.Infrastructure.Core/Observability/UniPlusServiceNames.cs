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
/// instanciar a sua <c>Program.cs</c>. Suites de teste hoje rodam com
/// <c>Observability:Enabled=false</c> (ver <c>ApiFactoryBase</c>) — quando uma
/// suite futura precisar reativar o pipeline OTel real, adicione um placeholder
/// específico aqui nesse momento (YAGNI por enquanto).</para>
/// </remarks>
public static class UniPlusServiceNames
{
    /// <summary>Módulo Seleção — API que orquestra editais, inscrições e classificação.</summary>
    public const string Selecao = "uniplus-selecao";

    /// <summary>Módulo Ingresso — API que orquestra chamadas de vagas, convocações e matrículas.</summary>
    public const string Ingresso = "uniplus-ingresso";

    /// <summary>Portal do candidato — API que orquestra perfil único e portfólio cross-módulo.</summary>
    public const string Portal = "uniplus-portal";

    /// <summary>Módulo OrganizacaoInstitucional — API que governa o roster fechado de áreas (CEPS, CRCA, PROEG, …).</summary>
    public const string OrganizacaoInstitucional = "uniplus-organizacao";
}
