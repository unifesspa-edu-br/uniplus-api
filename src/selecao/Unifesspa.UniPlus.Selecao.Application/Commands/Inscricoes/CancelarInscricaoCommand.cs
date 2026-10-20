namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Cancela a inscrição enviada do dono do token (`POST .../inscricao/cancelamento`), com o
/// <c>If-Match</c> corrente. O <paramref name="Motivo"/> é opcional.
/// </summary>
public sealed record CancelarInscricaoCommand(
    Guid ProcessoSeletivoId, string? Motivo, PrecondicaoIfMatch Precondicao) : ICommand<Result>;
