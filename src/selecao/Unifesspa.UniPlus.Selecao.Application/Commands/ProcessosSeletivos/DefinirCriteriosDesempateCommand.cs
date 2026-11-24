namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Item de entrada de um critério de desempate, usado por
/// <see cref="DefinirCriteriosDesempateCommand"/>: a ordem, a regra do catálogo que ele aplica e os
/// parâmetros dessa regra. Um critério sem <see cref="Parametros"/> equivale a um com todos eles ausentes.
/// </summary>
/// <param name="Ordem">Posição do critério na ordem de desempate.</param>
/// <param name="Regra">Referência à regra de desempate do catálogo.</param>
/// <param name="Parametros">Os parâmetros que a regra referenciada exige.</param>
public sealed record CriterioDesempateInput(
    int Ordem,
    ReferenciaRegraInput? Regra,
    ParametrosDoCriterioDesempateInput? Parametros);

/// <summary>
/// Os parâmetros aplicados por um critério de desempate, na forma do esquema de desempate do documento da configuração:
/// só o(s) relevante(s) para o código da regra referenciada vem(êm) preenchido(s).
/// <list type="bullet">
///   <item><description><c>DESEMPATE_MAIOR_NOTA_ETAPA</c>: <see cref="EtapaRef"/> (deve existir no processo).</description></item>
///   <item><description><c>DESEMPATE_IDOSO</c>: <see cref="IdadeMinima"/>.</description></item>
///   <item><description><c>DESEMPATE_CONDICAO_SOBRE_DADO</c>: <see cref="Fato"/>/<see cref="Operador"/>/<see cref="Valor"/>.</description></item>
///   <item><description><c>DESEMPATE_MAIOR_NOTA_AREA_ENEM</c>: <see cref="Areas"/>, os códigos das áreas do ENEM na ordem de desempate (devem existir no quadro de pesos por área congelado na classificação).</description></item>
///   <item><description><c>DESEMPATE_MAIOR_IDADE</c>: nenhum.</description></item>
/// </list>
/// </summary>
/// <param name="EtapaRef">Etapa do processo cuja nota desempata.</param>
/// <param name="IdadeMinima">Idade mínima, em anos, a partir da qual o candidato tem preferência.</param>
/// <param name="Fato">Código do fato do candidato que o predicado compara.</param>
/// <param name="Operador">Código do operador de comparação.</param>
/// <param name="Valor">Texto do valor da condição, interpretado como JSON quando for JSON válido.</param>
/// <param name="Areas">Códigos das áreas do ENEM, na ordem de desempate.</param>
public sealed record ParametrosDoCriterioDesempateInput(
    Guid? EtapaRef,
    int? IdadeMinima,
    string? Fato,
    [AceitaCodigosDe<Operador>] string? Operador,
    string? Valor,
    IReadOnlyList<string>? Areas = null)
{
    /// <summary>Os parâmetros de um critério enviado sem eles: todos ausentes.</summary>
    public static ParametrosDoCriterioDesempateInput Ausentes { get; } = new(null, null, null, null, null);
}

/// <summary>
/// Substitui integralmente os critérios de desempate do processo (Story
/// #774). Lista vazia remove todos os critérios: o rascunho aceita, mas a publicação de processo
/// com inscrição própria exige ao menos um (resultado importado dispensa).
/// </summary>
public sealed record DefinirCriteriosDesempateCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<CriterioDesempateInput> Criterios) : ICommand<Result>;
