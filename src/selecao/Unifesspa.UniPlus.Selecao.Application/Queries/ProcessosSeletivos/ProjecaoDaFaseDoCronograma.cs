namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Domain.ProcessosSeletivos.OrdemCanonica;
using Domain.ProcessosSeletivos.Recursos;
using Domain.ValueObjects;

using DTOs;

using Mappings;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Projeta para o contrato REST a fase do cronograma, com as bancas requeridas e a regra de recurso.
/// </summary>
internal static class ProjecaoDaFaseDoCronograma
{
    internal static IReadOnlyList<FaseCronogramaDto> ProjetarCronograma(ProcessoSeletivo processo) =>
        [.. OrdemCanonicaDaConfiguracao.Fases(processo.CronogramaFases).Select(ProjetarFase)];

    internal static FaseCronogramaDto ProjetarFase(FaseCronograma fase) => new(
        fase.Id,
        fase.Codigo,
        ProjetarFaseCanonica(fase),
        ProjetarCronogramaDaFase(fase),
        ProjetarPublicacoes(fase),
        ProjetarBancasRequeridas(fase));

    private static FaseCanonicaCongeladaDto ProjetarFaseCanonica(FaseCronograma fase) => new(
        OrigemId: fase.FaseCanonicaOrigemId,
        OrigemData: fase.OrigemData.ToCodigo(),
        AgrupaEtapas: fase.AgrupaEtapas,
        PermiteComplementacao: fase.PermiteComplementacao,
        ColetaInscricao: fase.ColetaInscricao,
        ColetaSolicitacaoIsencao: fase.ColetaSolicitacaoIsencao);

    private static CronogramaDaFaseDto ProjetarCronogramaDaFase(FaseCronograma fase) => new(
        Ordem: fase.Ordem,
        NoMesmoMomentoDaAnterior: fase.NoMesmoMomentoDaAnterior,
        Inicio: fase.Inicio,
        Fim: fase.Fim,
        UnidadeResponsavel: ProjetarUnidadeResponsavel(fase.UnidadeResponsavel));

    private static UnidadeResponsavelDto? ProjetarUnidadeResponsavel(UnidadeResponsavelSnapshot? unidade) =>
        unidade is null ? null : new UnidadeResponsavelDto(unidade.OrigemId, unidade.Sigla, unidade.Nome);

    private static PublicacoesDaFaseDto ProjetarPublicacoes(FaseCronograma fase) => new(
        Produtos: ProjetarProdutosDaFase(fase),
        ProduzResultado: fase.ProduzResultado,
        EmiteParecerIndividual: fase.EmiteParecerIndividual,
        RegraRecurso: fase.RegraRecurso is { } regraRecurso ? ProjetarRegraDeRecurso(regraRecurso) : null,
        TiposDeDecisao: [.. fase.TiposDeDecisao.Select(static tipo => tipo.Codigo())],
        RegraDeHomologacao: fase.RegraDeHomologacao is { } regraDeHomologacao ? ProjetarRegraDeHomologacao(regraDeHomologacao) : null);

    private static ProdutoDaFaseDto[] ProjetarProdutosDaFase(FaseCronograma fase) =>
        [.. OrdemCanonicaDaConfiguracao.ProdutosDaFase(fase.Produtos)
            .Select(static p => new ProdutoDaFaseDto(p.Id, p.AtoCodigo, p.Papel.ToCodigo()))];

    private static BancaRequeridaDto[] ProjetarBancasRequeridas(FaseCronograma fase) =>
        [.. fase.BancasRequeridas.Select(ProjetarBancaRequerida)];

    private static BancaRequeridaDto ProjetarBancaRequerida(BancaRequerida banca) => new(
        banca.Id,
        banca.TipoBancaOrigemId,
        banca.Codigo,
        banca.PosicaoNoTipo,
        banca.Nome,
        ProjecaoDaRegraDeDeliberacao.De(banca.RegraDeDeliberacao),
        [.. OrdemCanonicaDaConfiguracao.CandidatosAtendidos(banca.CandidatosAtendidos)
            .Select(static c => new CategoriaJulgadaDto(c.Id, c.CategoriaDocumentoOrigemId, c.Codigo))]);

    private static RegraDeHomologacaoDto ProjetarRegraDeHomologacao(RegraDeHomologacaoDasInscricoes regra) => new(
        regra.ResultadoAtoCodigo,
        [.. regra.Alternativas.Select(static alternativa => new AlternativaDeCondicoesDto(
            [.. alternativa.Condicoes.Select(static condicao => new CondicaoDeHomologacaoDto(
                condicao.Tipo.Codigo(),
                condicao.EdicaoDoExame,
                condicao.MotivoDesfavoravelCodigo,
                condicao.Enquadramento?.ToDto()))]))]);

    private static RegraRecursoFaseDto ProjetarRegraDeRecurso(RegraRecursoFase regraRecurso) => new(
        regraRecurso.Id,
        regraRecurso.ProdutoAncoraId,
        ProjecaoDoProcessoSeletivo.Referencia(regraRecurso.Regra),
        ProjecaoDoProcessoSeletivo.ProjetarParametrosDoPrazo(regraRecurso.Parametros),
        ProjecaoDosProcedimentosDeRecurso.De(regraRecurso.Procedimentos, OrigemDaNotaDaJanela.DaFase));
}
