namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Domain.Enums;
using Kernel.Results;

/// <summary>
/// Item de entrada de uma etapa pontuada, usado por
/// <see cref="DefinirEtapasCommand"/>.
/// </summary>
public sealed record EtapaProcessoInput(
    string Nome,
    CaraterEtapa Carater,
    decimal? Peso,
    decimal? NotaMinima,
    int? Ordem);

/// <summary>
/// Substitui integralmente as etapas pontuadas do processo (CA-02 da Story
/// #758). Etapas de caráter classificatória/ambas com peso compõem o divisor
/// da média.
/// </summary>
public sealed record DefinirEtapasCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<EtapaProcessoInput> Etapas) : ICommand<Result>;
