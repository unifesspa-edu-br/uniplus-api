namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Altera o motivo da sessão editorial em curso (ADR-0110 D5) — mutação como qualquer
/// outra: exige a precondição e incrementa a revisão.
/// </summary>
public sealed record AlterarMotivoRetificacaoCommand(
    Guid ProcessoSeletivoId,
    string Motivo,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
