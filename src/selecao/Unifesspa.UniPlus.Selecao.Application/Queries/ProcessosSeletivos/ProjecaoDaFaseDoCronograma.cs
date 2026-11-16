namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Mappings;

/// <summary>
/// Projeta para o contrato REST a fase do cronograma, com as bancas requeridas e a regra de recurso.
/// </summary>
internal static class ProjecaoDaFaseDoCronograma
{
    internal static FaseCronogramaDto ProjetarFase(FaseCronograma fase) => new(
        fase.Id,
        fase.Codigo,
        ProjetarFaseCanonica(fase),
        ProjetarCronograma(fase),
        ProjetarPublicacoes(fase),
        ProjetarBancasRequeridas(fase));

    private static FaseCanonicaCongeladaDto ProjetarFaseCanonica(FaseCronograma fase) => new(
        OrigemId: fase.FaseCanonicaOrigemId,
        OrigemData: fase.OrigemData.ToCodigo(),
        AgrupaEtapas: fase.AgrupaEtapas,
        PermiteComplementacao: fase.PermiteComplementacao,
        ColetaInscricao: fase.ColetaInscricao,
        ColetaSolicitacaoIsencao: fase.ColetaSolicitacaoIsencao);

    private static CronogramaDaFaseDto ProjetarCronograma(FaseCronograma fase) => new(
        Ordem: fase.Ordem,
        Inicio: fase.Inicio,
        Fim: fase.Fim,
        FaseConcluinteCodigo: fase.FaseConcluinteCodigo,
        UnidadeResponsavel: ProjetarUnidadeResponsavel(fase.UnidadeResponsavel));

    private static UnidadeResponsavelDto? ProjetarUnidadeResponsavel(UnidadeResponsavelSnapshot? unidade) =>
        unidade is null ? null : new UnidadeResponsavelDto(unidade.OrigemId, unidade.Sigla, unidade.Nome);

    private static PublicacoesDaFaseDto ProjetarPublicacoes(FaseCronograma fase) => new(
        Produtos: ProjetarProdutosDaFase(fase),
        ProduzResultado: fase.ProduzResultado,
        EmiteParecerIndividual: fase.EmiteParecerIndividual,
        RegraRecurso: fase.RegraRecurso is { } regraRecurso ? ProjetarRegraDeRecurso(regraRecurso) : null,
        TiposDeDecisao: [.. fase.TiposDeDecisao]);

    private static ProdutoDaFaseDto[] ProjetarProdutosDaFase(FaseCronograma fase) =>
        [.. fase.Produtos
            .OrderBy(static p => p.AtoCodigo, StringComparer.Ordinal)
            .Select(static p => new ProdutoDaFaseDto(p.Id, p.AtoCodigo, p.Papel.ToCodigo()))];

    // As bancas são ordenadas porque o código do tipo deixou de identificá-las sozinho: duas do mesmo
    // tipo convivem na mesma fase, e sem ordem a resposta sairia na sequência arbitrária em que o EF
    // materializou as linhas.
    private static BancaRequeridaDto[] ProjetarBancasRequeridas(FaseCronograma fase) =>
        [.. fase.BancasRequeridas
            .OrderBy(static b => b.Codigo, StringComparer.Ordinal)
            .ThenBy(static b => b.Id)
            .Select(ProjetarBancaRequerida)];

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
        ProjecaoDoProcessoSeletivo.ProjetarArgumentosDoPrazo(regraRecurso.Args));
}
