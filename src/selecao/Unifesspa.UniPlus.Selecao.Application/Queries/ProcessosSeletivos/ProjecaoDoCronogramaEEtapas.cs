namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Mappings;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Projeta para o contrato REST as etapas do processo e as fases do cronograma, com as bancas e as
/// regras de recurso de cada uma.
/// </summary>
internal static class ProjecaoDoCronogramaEEtapas
{
    internal static EtapaProcessoDto ProjetarEtapa(EtapaProcesso e) => new(
        e.Id, e.Nome, e.Carater,
        new TipoEtapaSnapshotDto(e.TipoEtapa.OrigemId, e.TipoEtapa.Codigo, e.TipoEtapa.Nome, e.TipoEtapa.NotaDeOrigemNoEnem),
        e.Peso, e.NotaMinima, e.NotaMaxima, e.PassoDoLancamento,
        e.Ordem, e.FaseCodigo,
        [.. e.Produtos.Select(pr => new ProdutoDaEtapaDto(pr.Id, pr.AtoCodigo, pr.Papel.ToCodigo()))],
        e.Inicio, e.Fim, e.EmiteParecerIndividual,
        [.. e.Bancas.Select(ProjetarBancaDaEtapa)],
        [.. e.Recursos.Select(r => ProjetarRecursoDaEtapa(e, r))],
        e.FormaDeAvaliacao.ToCodigo(),
        e.RegraConsolidacao is { } consolidacao ? ProjecaoDoProcessoSeletivo.Referencia(consolidacao) : null,
        [.. e.ItensDeAvaliacao.Select(i => new ItemDeAvaliacaoDto(i.Id, i.Rotulo, i.PontuacaoMaxima))],
        ProjetarEscala(e));

    private static BancaDaEtapaDto ProjetarBancaDaEtapa(BancaDaEtapa banca) => new(
        banca.Id,
        banca.TipoBancaOrigemId,
        banca.Codigo,
        banca.Nome,
        ProjecaoDaRegraDeDeliberacao.De(banca.RegraDeDeliberacao));

    private static NivelDeAtendimentoDto[]? ProjetarEscala(EtapaProcesso e) =>
        e.EscalaDeAtendimento is { } escala
            ? [.. escala.Select(n => new NivelDeAtendimentoDto(n.Rotulo, n.Fracao))]
            : null;

    // O código do ato âncora acompanha a leitura: sem ele, o cliente teria de cruzar o id do produto à
    // mão para reexibir contra o que o recurso corre.
    private static RecursoDaEtapaDto ProjetarRecursoDaEtapa(EtapaProcesso etapa, RecursoDaEtapa recurso) => new(
        recurso.Id,
        recurso.Ancora,
        ProjecaoDoProcessoSeletivo.Referencia(recurso.Regra),
        ProjetarArgumentosDoPrazo(recurso.Args),
        recurso.ProdutoAncoraId,
        etapa.Produtos.FirstOrDefault(pr => pr.Id == recurso.ProdutoAncoraId)?.AtoCodigo);

    private static ArgsRegraPrazoRecursoDto ProjetarArgumentosDoPrazo(ArgsRegraPrazoRecurso args) => new(
        args.PrazoValor,
        args.PrazoUnidade,
        args.SuspensividadePrimeiraInstanciaValor,
        args.SuspensividadePrimeiraInstanciaUnidade,
        args.AdmiteInstanciaSuperior,
        args.SuspensividadeSegundaInstanciaValor,
        args.SuspensividadeSegundaInstanciaUnidade);

    internal static FaseCronogramaDto ProjetarFase(FaseCronograma fase) => new(
        fase.Id,
        fase.Ordem,
        fase.FaseCanonicaOrigemId,
        fase.Codigo,
        fase.OrigemData.ToCodigo(),
        fase.AgrupaEtapas,
        fase.PermiteComplementacao,
        fase.ProduzResultado,
        fase.ColetaInscricao,
        fase.ColetaSolicitacaoIsencao,
        fase.Inicio,
        fase.Fim,
        [.. fase.Produtos
            .OrderBy(static p => p.AtoCodigo, StringComparer.Ordinal)
            .Select(static p => new ProdutoDaFaseDto(p.Id, p.AtoCodigo, p.Papel.ToCodigo()))],
        fase.FaseConcluinteCodigo,
        fase.EmiteParecerIndividual,
        [.. fase.BancasRequeridas
            .OrderBy(static b => b.Codigo, StringComparer.Ordinal)
            .ThenBy(static b => b.Id)
            .Select(ProjetarBancaRequerida)],
        fase.RegraRecurso is { } regraRecurso ? ProjetarRegraDeRecurso(regraRecurso) : null,
        fase.UnidadeResponsavel is { } unidade ? new UnidadeResponsavelDto(unidade.OrigemId, unidade.Sigla, unidade.Nome) : null);

    // As bancas são ordenadas porque o código do tipo deixou de identificá-las sozinho: duas do mesmo
    // tipo convivem na mesma fase, e sem ordem a resposta sairia na sequência arbitrária em que o EF
    // materializou as linhas.
    private static BancaRequeridaDto ProjetarBancaRequerida(BancaRequerida banca) => new(
        banca.Id,
        banca.TipoBancaOrigemId,
        banca.Codigo,
        banca.Nome,
        ProjecaoDaRegraDeDeliberacao.De(banca.RegraDeDeliberacao),
        [.. banca.RecorteDeCompetencia
            .OrderBy(static c => c.Codigo, StringComparer.Ordinal)
            .Select(static c => new CategoriaJulgadaDto(c.Id, c.CategoriaDocumentoOrigemId, c.Codigo))]);

    private static RegraRecursoFaseDto ProjetarRegraDeRecurso(RegraRecursoFase regraRecurso) => new(
        regraRecurso.Id,
        regraRecurso.ProdutoAncoraId,
        ProjecaoDoProcessoSeletivo.Referencia(regraRecurso.Regra),
        ProjetarArgumentosDoPrazo(regraRecurso.Args));
}
