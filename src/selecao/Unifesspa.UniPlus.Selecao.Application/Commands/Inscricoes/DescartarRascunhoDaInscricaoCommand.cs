namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Descarta o rascunho da inscrição do dono do token (`DELETE .../inscricao`): remove a inscrição e as
/// linhas filhas. Vale só para rascunho, com o <c>If-Match</c> corrente.
/// </summary>
public sealed record DescartarRascunhoDaInscricaoCommand(
    Guid ProcessoSeletivoId, PrecondicaoIfMatch Precondicao) : ICommand<Result>;
