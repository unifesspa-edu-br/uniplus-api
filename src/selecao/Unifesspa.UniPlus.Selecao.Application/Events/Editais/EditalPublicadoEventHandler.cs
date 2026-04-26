namespace Unifesspa.UniPlus.Selecao.Application.Events.Editais;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Selecao.Domain.Events;

/// <summary>
/// Handler exemplar do <see cref="EditalPublicadoEvent"/> — registra o evento
/// drenado via cascading messages (ADR-026). Demonstra o padrão para
/// subscritores de domain events do UniPlus: handler convention-based, método
/// <c>Handle</c> público, dependências por parâmetro do método. Logging via
/// <c>[LoggerMessage]</c> source generator (regra obrigatória — ver CLAUDE.md
/// do uniplus-api).
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção do projeto: subscritores de domain events terminam em EventHandler, conforme AC da #136.")]
public sealed partial class EditalPublicadoEventHandler
{
    public static void Handle(
        EditalPublicadoEvent @event,
        ILogger<EditalPublicadoEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(logger);

        LogEditalPublicadoRecebido(logger, @event.EditalId, @event.NumeroEdital);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "EditalPublicadoEvent recebido. EditalId={EditalId} NumeroEdital={NumeroEdital}")]
    private static partial void LogEditalPublicadoRecebido(
        ILogger logger,
        Guid editalId,
        string numeroEdital);
}
