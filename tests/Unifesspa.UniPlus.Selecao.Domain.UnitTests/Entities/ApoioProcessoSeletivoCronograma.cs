namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioProcessoSeletivoCronograma
{
    internal static ProcessoSeletivo NovoProcesso(OrigemCandidatos origem = OrigemCandidatos.ImportacaoExterna) =>
        ProcessoSeletivo.Criar("PS Cronograma", TiposDeProcessoDeTeste.SiSU, origem, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal static Result<FaseCronograma> Fase(
        int ordem,
        string codigo,
        bool agrupaEtapas = false,
        bool produzResultado = false,
        bool coletaInscricao = false,
        DateTimeOffset? inicio = null,
        DateTimeOffset? fim = null,
        Guid? faseCanonicaOrigemId = null,
        bool coletaSolicitacaoIsencao = false,
        IReadOnlyList<ProdutoDaFase>? produtos = null,
        string? faseConcluinteCodigo = null,
        bool emiteParecerIndividual = false,
        IReadOnlyList<BancaRequerida>? bancasRequeridas = null) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                faseCanonicaOrigemId ?? Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, agrupaEtapas, PermiteComplementacao: false, coletaInscricao, coletaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(ordem, inicio, fim, faseConcluinteCodigo, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(produtos ?? (produzResultado ? [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)] : []), emiteParecerIndividual, RegraRecurso: null),
            bancasRequeridas ?? []);

    internal static (FaseCronograma Inscricao, FaseCronograma Homologacao) InscricaoEHomologacaoComJanelasSobrepostas()
    {
        FaseCronograma inscricao = Fase(
            1, "INSCRICAO", coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)).Value!;
        FaseCronograma homologacao = Fase(
            2, "HOMOLOGACAO_INSCRICOES",
            inicio: new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero)).Value!;
        return (inscricao, homologacao);
    }

    internal static IReadOnlyList<ProdutoDaFase> Preliminar(string atoCodigo = "RESULTADO_CLASSIFICACAO") =>
        [ProdutoDaFase.Criar(atoCodigo, PapelProdutoFase.Preliminar)];

    internal static IReadOnlyList<ProdutoDaFase> Definitiva(string atoCodigo = "RESULTADO_CLASSIFICACAO") =>
        [ProdutoDaFase.Criar(atoCodigo, PapelProdutoFase.Definitivo)];
}
