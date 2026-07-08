namespace Unifesspa.UniPlus.Selecao.Application.Events.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Selecao.Domain.Events;

/// <summary>
/// Handler exemplar do <see cref="ProcessoPublicadoEvent"/> — registra o evento
/// drenado via cascading messages (ADR-0005/ADR-0041, Story #759 T4 #785). Slice
/// canônico de referência para subscritores de domain events do UniPlus: handler
/// convention-based, método <c>Handle</c> público, dependências por parâmetro do
/// método. Logging via <c>[LoggerMessage]</c> source generator (regra obrigatória
/// do projeto — chamadas diretas a <c>logger.LogX(...)</c> são bloqueadas pelo
/// analisador CA1848 com <c>TreatWarningsAsErrors</c>).
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção do projeto: subscritores de domain events terminam em EventHandler, conforme AC da #136.")]
public sealed partial class ProcessoPublicadoEventHandler
{
    public static void Handle(
        ProcessoPublicadoEvent @event,
        ILogger<ProcessoPublicadoEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(logger);

        LogProcessoPublicadoRecebido(logger, @event.ProcessoSeletivoId, @event.EditalId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "ProcessoPublicadoEvent recebido. ProcessoSeletivoId={ProcessoSeletivoId} EditalId={EditalId}")]
    private static partial void LogProcessoPublicadoRecebido(
        ILogger logger,
        Guid processoSeletivoId,
        Guid editalId);
}
