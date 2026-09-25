namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara, troca ou remove o identificador legível do processo seletivo (issue #1479). Aceito só
/// enquanto o processo nunca foi publicado.
/// </summary>
/// <param name="IdentificadorLegivel">Valor em kebab-case; ausente remove a declaração.</param>
public sealed record DefinirIdentificadorLegivelCommand(
    Guid ProcessoSeletivoId,
    string? IdentificadorLegivel,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
