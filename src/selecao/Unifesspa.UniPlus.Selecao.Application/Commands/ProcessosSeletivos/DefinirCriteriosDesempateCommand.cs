namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Item de entrada de um critério de desempate, usado por
/// <see cref="DefinirCriteriosDesempateCommand"/>. Os campos além de
/// <see cref="Ordem"/>/<see cref="RegraCodigo"/>/<see cref="RegraVersao"/> são
/// os args aplicados — apenas o(s) relevante(s) para o código da regra
/// referenciada é(são) preenchido(s):
/// <list type="bullet">
///   <item><description><c>DESEMPATE-MAIOR-NOTA-ETAPA</c>: <see cref="EtapaRef"/> (deve existir no processo, INV-B6).</description></item>
///   <item><description><c>DESEMPATE-IDOSO</c>: <see cref="IdadeMinima"/>.</description></item>
///   <item><description><c>DESEMPATE-PREDICADO-FATO</c>: <see cref="Fato"/>/<see cref="Operador"/>/<see cref="Valor"/>.</description></item>
///   <item><description><c>DESEMPATE-MAIOR-NOTA-AREA-ENEM</c>: <see cref="Areas"/>, os códigos das áreas do ENEM na ordem de desempate (devem existir no quadro de pesos por área congelado na classificação).</description></item>
///   <item><description><c>DESEMPATE-MAIOR-IDADE</c>: nenhum.</description></item>
/// </list>
/// </summary>
public sealed record CriterioDesempateInput(
    int Ordem,
    string RegraCodigo,
    string RegraVersao,
    Guid? EtapaRef,
    int? IdadeMinima,
    string? Fato,
    string? Operador,
    string? Valor,
    IReadOnlyList<string>? Areas = null);

/// <summary>
/// Substitui integralmente os critérios de desempate do processo (Story
/// #774). Dimensão opcional (0..*) — lista vazia remove
/// todos os critérios.
/// </summary>
public sealed record DefinirCriteriosDesempateCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<CriterioDesempateInput> Criterios,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
