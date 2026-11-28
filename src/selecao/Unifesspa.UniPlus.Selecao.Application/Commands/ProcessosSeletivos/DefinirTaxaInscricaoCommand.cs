namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Define a taxa de inscrição e os fundamentos de isenção do processo. <c>Cobra</c> é obrigatório: ausente
/// ou nulo é recusado (422), e não remove a declaração. <c>Fundamentos</c> é obrigatório quando <c>Cobra</c> == <see langword="true"/> (issue #1310).
/// </summary>
public sealed record DefinirTaxaInscricaoCommand(
    Guid ProcessoSeletivoId,
    bool? Cobra,
    decimal? Valor,
    IReadOnlyList<string>? Fundamentos) : ICommand<Result>;
