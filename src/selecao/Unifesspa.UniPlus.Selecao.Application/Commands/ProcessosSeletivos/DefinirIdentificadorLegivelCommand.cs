namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara ou troca o identificador legível do processo seletivo. Só em rascunho.
/// </summary>
/// <param name="IdentificadorLegivel">Valor em kebab-case, obrigatório: não há remoção.</param>
public sealed record DefinirIdentificadorLegivelCommand(
    Guid ProcessoSeletivoId,
    string? IdentificadorLegivel) : ICommand<Result>;
