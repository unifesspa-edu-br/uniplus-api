namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Item de entrada de uma etapa pontuada, usado por
/// <see cref="DefinirEtapasCommand"/>. <see cref="Id"/> é opcional: quando
/// informado e corresponder a uma etapa já existente no processo, o handler
/// atualiza a MESMA etapa em vez de recriá-la — preservando a identidade que
/// critérios de desempate ou regras de eliminação da classificação possam
/// referenciar (<c>etapa_ref</c>). Omitido (ou sem correspondência), o
/// handler cria uma etapa nova.
/// </summary>
public sealed record EtapaProcessoInput(
    string Nome,
    CaraterEtapa Carater,
    decimal? Peso,
    decimal? NotaMinima,
    int? Ordem,
    Guid? Id = null);

/// <summary>
/// Substitui integralmente as etapas pontuadas do processo (CA-02 da Story
/// #758). Etapas de caráter classificatória/ambas com peso compõem o divisor
/// da média.
/// </summary>
public sealed record DefinirEtapasCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<EtapaProcessoInput> Etapas,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
