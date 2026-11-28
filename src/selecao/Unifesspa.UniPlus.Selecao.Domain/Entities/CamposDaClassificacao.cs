namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a classificação declara, no ponto em que <see cref="ConfiguracaoClassificacao.ConferirCampos"/> a confere.
/// Cada parte que depende de um cadastro externo pode chegar vazia: a fórmula da nota quando o catálogo não a
/// confirmou, e as eliminações que o catálogo ou os parâmetros recusaram, que ficam vazias na posição em que vieram.
/// A conferência pula só o que depende dessas partes, para não recusar por consequência.
/// </summary>
public sealed record CamposDaClassificacao
{
    /// <summary>Fórmula da nota; nula quando o catálogo não confirmou a regra citada.</summary>
    public ReferenciaRegra? RegraCalculo { get; init; }

    /// <summary>
    /// A regra de arredondamento foi informada, resolvida ou não: a obrigatoriedade olha o que o operador declarou,
    /// não o que o catálogo confirmou.
    /// </summary>
    public bool ArredondamentoDeclarado { get; init; }

    /// <summary>Casas decimais da precisão.</summary>
    public int? CasasArredondamento { get; init; }

    /// <summary>Eliminações declaradas; nulas quando o pedido não informou a lista.</summary>
    public EliminacoesDeclaradas? Eliminacoes { get; init; }

    /// <summary>A classificação usa a estrutura de pontuação por área do ENEM; nulo quando o pedido não informou.</summary>
    public bool? BaseadoEmEnem { get; init; }

    /// <summary>Resolução de Pesos por Área declarada, como o operador a digitou.</summary>
    public string? ResolucaoPesoAreaEnem { get; init; }

    /// <summary>Cópia por valor da resolução declarada, um item por grupo de área.</summary>
    public IReadOnlyList<GrupoPesoAreaEnemCongelado> QuadroPesoAreaEnem { get; init; } = [];

    /// <summary>Escala em que a nota final é publicada.</summary>
    public decimal? EscalaNotaFinal { get; init; }
}
