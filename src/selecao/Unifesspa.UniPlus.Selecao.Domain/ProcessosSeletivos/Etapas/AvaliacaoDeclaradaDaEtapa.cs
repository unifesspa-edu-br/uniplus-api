namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Como a etapa declara a avaliação: o lançamento da nota, os itens e o conceito, quando há.</summary>
public sealed record AvaliacaoDeclaradaDaEtapa(
    DeclaracaoDaAvaliacaoDaEtapa Declaracao,
    IReadOnlyList<ItemDeAvaliacaoDeclarado> Itens,
    ConceitoDaEtapa? Conceito);
