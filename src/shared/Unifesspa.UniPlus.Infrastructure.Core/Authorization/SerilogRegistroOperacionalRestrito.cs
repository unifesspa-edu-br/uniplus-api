namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Collections.Generic;

using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Sinks.OpenTelemetry;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Logging;
using Unifesspa.UniPlus.Infrastructure.Core.Observability;

/// <summary>
/// Grava as decisões de acesso num fluxo <b>dedicado</b> — um pipeline Serilog
/// próprio, com exportador OpenTelemetry sob <c>service.name</c> distinto —,
/// separado do pipeline comum da aplicação (<c>SerilogConfiguration</c>).
/// </summary>
/// <remarks>
/// <para>
/// A separação é o ponto: o uso de uma permissão não deve se misturar à
/// telemetria geral, onde qualquer pessoa com acesso ao painel de logs o leria.
/// O <see cref="Serilog.Core.Logger"/> é instanciado aqui e não é o
/// <c>Log.Logger</c> estático, de modo que nenhum <i>enricher</i> ou sink do
/// pipeline global alcança estes eventos — nem eles, os de lá.
/// </para>
/// <para>
/// O destino é o mesmo coletor que a aplicação já usa, sob rótulo próprio, e não
/// um arquivo local: num contêiner, o arquivo não sobrevive ao pod e ninguém o
/// coleta — o registro existiria apenas nominalmente. Com o rótulo próprio, a
/// restrição de leitura passa a ser configuração do backend de observabilidade,
/// que é onde ela pode evoluir (fluxo separado hoje, isolamento por inquilino
/// quando houver) sem tocar neste código.
/// </para>
/// <para>
/// <b>Quem registra não decide.</b> O Serilog absorve, por desenho, qualquer
/// falha de escrita do sink e a reporta apenas no seu diagnóstico interno
/// (<c>SelfLog</c>) — nenhuma exceção escapa de uma chamada de log, nem com o
/// pipeline já descartado. Um coletor indisponível, portanto, não altera o
/// veredito nem derruba a requisição; o que se perde é o registro.
/// </para>
/// <para>
/// Não é a trilha de auditoria da ADR-0086: não há append-only nem código de
/// autenticação de mensagem. O destino durável e verificável é trabalho próprio.
/// </para>
/// </remarks>
public sealed class SerilogRegistroOperacionalRestrito : IRegistroOperacionalRestrito, IDisposable
{
    private readonly Serilog.ILogger? _destinoDedicado;
    private readonly Serilog.Core.Logger? _pipelineProprio;

    /// <summary>Cria o registro e o seu pipeline dedicado.</summary>
    public SerilogRegistroOperacionalRestrito(IOptions<RegistroOperacionalRestritoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RegistroOperacionalRestritoOptions valores = options.Value;
        if (!valores.Habilitado)
        {
            return;
        }

        Serilog.Core.Logger pipeline = ConstruirPipelineDedicado(valores.NomeServico);
        _destinoDedicado = pipeline;
        _pipelineProprio = pipeline;
    }

    // Construtor de teste: recebe o destino pronto, para que as suítes observem o
    // que foi gravado sem depender de um coletor de verdade.
    internal SerilogRegistroOperacionalRestrito(Serilog.ILogger destinoDedicado)
        => _destinoDedicado = destinoDedicado;

    /// <summary>
    /// O fluxo dedicado está instalado e recebendo decisões. Falso quando o
    /// registro foi desligado por configuração ou porque não há coletor — caso em
    /// que a decisão continua sendo tomada e aplicada, apenas não é exportada.
    /// </summary>
    public bool Ativo => _destinoDedicado is not null;

    /// <inheritdoc />
    public void Registrar(RegistroDecisaoAcesso registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        _destinoDedicado?.Information("{@Decisao}", registro);
    }

    /// <inheritdoc />
    public void Dispose() => _pipelineProprio?.Dispose();

    /// <summary>
    /// Enriquecimento do fluxo dedicado. Separado do destino porque é o que as
    /// suítes precisam exercitar: aplicá-lo a uma configuração cujo destino elas
    /// controlam prova o tratamento que o pipeline de produção dá aos eventos,
    /// sem depender do coletor nem de vasculhar o objeto já construído.
    /// </summary>
    /// <remarks>
    /// O identificador do sujeito é opaco por contrato, mas quem o emite é o
    /// provedor de identidade, não este código: um provedor que use o CPF como
    /// <i>subject</i> faria o registro exportar dado pessoal. O mascaramento do
    /// pipeline comum (ADR-0011) vale aqui pelo mesmo motivo — e, por ser um
    /// pipeline à parte, precisa ser declarado à parte.
    /// </remarks>
    internal static LoggerConfiguration Enriquecer(LoggerConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return configuracao.Enrich.With<PiiMaskingEnricher>();
    }

    // Pipeline próprio: só o exportador OTLP, sem os sinks do pipeline comum. O
    // service.name distinto é o que permite ao backend de observabilidade tratar
    // este fluxo à parte — inclusive quanto a quem o lê.
    private static Serilog.Core.Logger ConstruirPipelineDedicado(string nomeServico) =>
        Enriquecer(new LoggerConfiguration())
            .WriteTo.OpenTelemetry(options =>
            {
                options.Protocol = OtlpProtocol.Grpc;
                options.IncludedData =
                    IncludedData.TraceIdField
                    | IncludedData.SpanIdField
                    | IncludedData.MessageTemplateTextAttribute;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = nomeServico,
                    ["service.namespace"] = OpenTelemetryConfiguration.ServiceNamespaceResourceValue,
                };
            })
            .CreateLogger();
}
