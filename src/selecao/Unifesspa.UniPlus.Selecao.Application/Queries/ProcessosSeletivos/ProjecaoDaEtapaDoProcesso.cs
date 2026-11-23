namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ProcessosSeletivos.OrdemCanonica;
using Domain.ProcessosSeletivos.Recursos;
using Domain.ValueObjects;

using DTOs;

using Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Projeta para o contrato REST a etapa do processo, com as bancas e os recursos que ela declara.
/// </summary>
internal static class ProjecaoDaEtapaDoProcesso
{
    internal static IReadOnlyList<EtapaProcessoDto> ProjetarEtapas(ProcessoSeletivo processo) =>
        [.. OrdemCanonicaDaConfiguracao.Etapas(processo.Etapas).Select(ProjetarEtapa)];

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
        [.. etapa.ItensDeAvaliacao.Select(i => new ItemDeAvaliacaoDto(i.Id, i.Rotulo, i.PontuacaoMaxima, i.Ordem))],
        ProjetarEscala(etapa));

    private static PublicacoesDaEtapaDto ProjetarPublicacoes(EtapaProcesso etapa) => new(
        [.. OrdemCanonicaDaConfiguracao.ProdutosDaEtapa(etapa.Produtos).Select(pr => new ProdutoDaEtapaDto(pr.Id, pr.AtoCodigo, pr.Papel.ToCodigo()))],
        etapa.EmiteParecerIndividual,
        [.. etapa.Recursos.Select(r => ProjetarRecursoDaEtapa(etapa, r))],
        [.. etapa.TiposDeDecisao.Select(CodigoComRotulo<TipoMotivoDeDecisao>.De)]);

    private static BancaDaEtapaDto ProjetarBancaDaEtapa(BancaDaEtapa banca) => new(
        banca.Id,
        banca.Tipo.OrigemId,
        CodigoComRotulo<TipoDeBanca>.De(banca.Tipo.Codigo),
        banca.PosicaoNoTipo,
        banca.Tipo.Nome,
        CodigoComRotulo<PapelDaBanca>.De(banca.Tipo.Papel),
        banca.Tipo.ExigeDuasOuMaisBancas,
        banca.Tipo.EncerraRecursoNaPrimeiraInstancia,
        [.. banca.Tipo.TiposDeDecisaoJulgados.Select(CodigoComRotulo<TipoMotivoDeDecisao>.De)],
        ProjecaoDaRegraDeDeliberacao.De(banca.RegraDeDeliberacao));

    private static NivelDeAtendimentoDto[]? ProjetarEscala(EtapaProcesso etapa) =>
        etapa.EscalaDeAtendimento is { } escala
            ? [.. escala.Select(n => new NivelDeAtendimentoDto(n.Rotulo, n.Fracao, n.Ordem))]
            : null;

    // O código do ato âncora acompanha a leitura: sem ele, o cliente teria de cruzar o id do produto à
    // mão para reexibir contra o que o recurso corre.
    private static RecursoDaEtapaDto ProjetarRecursoDaEtapa(EtapaProcesso etapa, RecursoDaEtapa recurso) => new(
        recurso.Id,
        recurso.Ancora,
        ProjecaoDoProcessoSeletivo.Referencia(recurso.Regra),
        ProjecaoDoProcessoSeletivo.ProjetarParametrosDoPrazo(recurso.Parametros),
        recurso.ProdutoAncoraId,
        etapa.Produtos.FirstOrDefault(pr => pr.Id == recurso.ProdutoAncoraId)?.AtoCodigo,
        ProjecaoDosProcedimentosDeRecurso.De(recurso.Procedimentos, new(etapa.FormaDeAvaliacao, etapa.DeclaraNotaDoEnem)));
}
