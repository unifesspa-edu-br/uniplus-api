namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Salva o rascunho da inscrição (issue #1859, `PUT .../inscricao`): validação estrutural, sem
/// exigir obrigatórios — a validação completa (obrigatórios, impedimentos, termos, grupos) é da
/// submissão.
/// </summary>
public sealed record SalvarInscricaoCommand(
    Guid ProcessoSeletivoId, InscricaoRequest Request, PrecondicaoIfMatch Precondicao) : ICommand<Result<InscricaoDto>>;
