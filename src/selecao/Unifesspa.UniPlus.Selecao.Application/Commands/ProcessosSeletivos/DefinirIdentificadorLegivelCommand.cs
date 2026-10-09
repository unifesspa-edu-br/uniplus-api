namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara ou troca o identificador legível do processo seletivo. Aceito enquanto ele não consta
/// em versão publicada: em rascunho, ou na sessão de retificação aberta sobre versão congelada
/// sem ele.
/// </summary>
/// <param name="IdentificadorLegivel">Valor em kebab-case, obrigatório: não há remoção.</param>
public sealed record DefinirIdentificadorLegivelCommand(
    Guid ProcessoSeletivoId,
    string? IdentificadorLegivel,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
