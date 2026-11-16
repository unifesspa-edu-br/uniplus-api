namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Mappings;

/// <summary>
/// Projeta para o contrato REST a etapa do processo, com as bancas e os recursos que ela declara.
/// </summary>
internal static class ProjecaoDaEtapaDoProcesso
{
    internal static EtapaProcessoDto ProjetarEtapa(EtapaProcesso etapa) => new(
        etapa.Id,
        ProjetarIdentificacao(etapa),
        ProjetarNota(etapa),
        ProjetarAvaliacao(etapa),
        new JanelaDaEtapaDto(Inicio: etapa.Inicio, Fim: etapa.Fim),
        ProjetarPublicacoes(etapa),
        [.. etapa.Bancas.Select(ProjetarBancaDaEtapa)]);

    private static IdentificacaoDaEtapaDto ProjetarIdentificacao(EtapaProcesso etapa) => new(
        etapa.Nome,
        etapa.Carater,
        ProjetarTipoDeEtapa(etapa.TipoEtapa),
        etapa.Ordem,
        etapa.FaseCodigo);

    private static TipoEtapaSnapshotDto ProjetarTipoDeEtapa(TipoEtapaSnapshot tipo) =>
        new(tipo.OrigemId, tipo.Codigo, tipo.Nome, tipo.NotaDeOrigemNoEnem, tipo.ExigeCorrecaoIndependente);

    private static NotaDaEtapaDto ProjetarNota(EtapaProcesso etapa) => new(
        Peso: etapa.Peso,
        NotaMinima: etapa.NotaMinima,
        NotaMaxima: etapa.NotaMaxima,
        PassoDoLancamento: etapa.PassoDoLancamento);

    private static AvaliacaoDaEtapaDto ProjetarAvaliacao(EtapaProcesso etapa) => new(
        etapa.FormaDeAvaliacao.ToCodigo(),
        etapa.RegraConsolidacao is { } consolidacao ? ProjecaoDoProcessoSeletivo.Referencia(consolidacao) : null,
        [.. etapa.ItensDeAvaliacao.Select(i => new ItemDeAvaliacaoDto(i.Id, i.Rotulo, i.PontuacaoMaxima))],
        ProjetarEscala(etapa));

    private static PublicacoesDaEtapaDto ProjetarPublicacoes(EtapaProcesso etapa) => new(
        [.. etapa.Produtos.Select(pr => new ProdutoDaEtapaDto(pr.Id, pr.AtoCodigo, pr.Papel.ToCodigo()))],
        etapa.EmiteParecerIndividual,
        [.. etapa.Recursos.Select(r => ProjetarRecursoDaEtapa(etapa, r))],
        [.. etapa.TiposDeDecisao]);

    private static BancaDaEtapaDto ProjetarBancaDaEtapa(BancaDaEtapa banca) => new(
        banca.Id,
        banca.TipoBancaOrigemId,
        banca.Codigo,
        banca.Nome,
        ProjecaoDaRegraDeDeliberacao.De(banca.RegraDeDeliberacao));

    private static NivelDeAtendimentoDto[]? ProjetarEscala(EtapaProcesso etapa) =>
        etapa.EscalaDeAtendimento is { } escala
            ? [.. escala.Select(n => new NivelDeAtendimentoDto(n.Rotulo, n.Fracao))]
            : null;

    // O código do ato âncora acompanha a leitura: sem ele, o cliente teria de cruzar o id do produto à
    // mão para reexibir contra o que o recurso corre.
    private static RecursoDaEtapaDto ProjetarRecursoDaEtapa(EtapaProcesso etapa, RecursoDaEtapa recurso) => new(
        recurso.Id,
        recurso.Ancora,
        ProjecaoDoProcessoSeletivo.Referencia(recurso.Regra),
        ProjecaoDoProcessoSeletivo.ProjetarArgumentosDoPrazo(recurso.Args),
        recurso.ProdutoAncoraId,
        etapa.Produtos.FirstOrDefault(pr => pr.Id == recurso.ProdutoAncoraId)?.AtoCodigo,
        ProjecaoDosProcedimentosDeRecurso.De(recurso.Procedimentos));
}
