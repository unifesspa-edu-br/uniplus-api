namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Ordenacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A etapa como está agora, lida como se acabasse de ser declarada: a âncora de uma janela recursal pode ter ficado sem o que a
/// sustentava, e a hidratação do EF materializa estado violado. O conceito não é parte da entidade e fica ausente.
/// </summary>
internal static class EtapaComoDeclarada
{
    internal static EtapaDeclarada De(EtapaProcesso etapa, IReadOnlySet<string>? colecoesComParteRecusada) => EtapaDeclarada.Declarar(
        etapa.Id,
        new IdentificacaoDaEtapa(etapa.Nome, etapa.Carater, etapa.TipoEtapa, etapa.Ordem, etapa.FaseCodigo),
        new AvaliacaoDeclaradaDaEtapa(Avaliacao(etapa), [.. etapa.ItensDeAvaliacao.Select(static item => new ItemDeAvaliacaoDeclarado(item.Id, item.Rotulo, item.PontuacaoMaxima))], null),
        new JanelaDeTempo(etapa.Inicio, etapa.Fim),
        Publicacoes(etapa),
        [.. etapa.Bancas],
        colecoesComParteRecusada);

    private static DeclaracaoDaAvaliacaoDaEtapa Avaliacao(EtapaProcesso etapa) => new(
        etapa.Peso, etapa.NotaMinima, etapa.NotaMaxima, etapa.PassoDoLancamento, etapa.FormaDeAvaliacao, etapa.RegraConsolidacao, EscalaDeAtendimento(etapa));

    private static List<NivelDeAtendimentoDeclarado>? EscalaDeAtendimento(EtapaProcesso etapa) =>
        etapa.EscalaDeAtendimento is { } escala ? [.. escala.Select(static nivel => new NivelDeAtendimentoDeclarado(nivel.Rotulo, nivel.Fracao, nivel.Chave.Valor))] : null;

    private static PublicacoesDaEtapa Publicacoes(EtapaProcesso etapa) => new(
        [.. OrdemDaConfiguracao.ProdutosDaEtapa(etapa.Produtos)],
        etapa.EmiteParecerIndividual,
        [.. etapa.Recursos],
        [.. etapa.TiposDeDecisao.Select(static tipo => tipo.Codigo())]);
}
