namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

/// <summary>
/// O que a etapa publica, o parecer individual que essas publicações sustentam e as janelas recursais que ela
/// abre. É entrada de vida curta, superficialmente imutável: carrega as instâncias de
/// <see cref="ProdutoDaEtapa"/> e <see cref="RecursoDaEtapa"/> e nunca é guardada pela declaração da etapa.
/// </summary>
/// <param name="TiposDeDecisao">Os tipos de decisão que a etapa declara produzir, como chegaram.</param>
public sealed record PublicacoesDaEtapa(
    IReadOnlyList<ProdutoDaEtapa> Produtos,
    bool EmiteParecerIndividual,
    IReadOnlyList<RecursoDaEtapa> Recursos,
    IReadOnlyList<string?>? TiposDeDecisao = null);
