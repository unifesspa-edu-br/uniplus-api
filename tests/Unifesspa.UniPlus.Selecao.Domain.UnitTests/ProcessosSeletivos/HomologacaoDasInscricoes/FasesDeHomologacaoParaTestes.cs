namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class FasesDeHomologacaoParaTestes
{
    internal const string ResultadoDaLista = "RESULTADO_HOMOLOGACAO";

    internal static FaseCronograma Fase(
        int ordem, string codigo, IReadOnlyList<ProdutoDaFase>? produtos = null, RegraRecursoFase? regraRecurso = null,
        IReadOnlyList<string?>? tiposDeDecisao = null,
        RegraDeHomologacaoDasInscricoes? regraDeHomologacao = null) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(produtos ?? [], EmiteParecerIndividual: false, regraRecurso, tiposDeDecisao, regraDeHomologacao),
            regraRecurso is null ? [] : [BancasDeTeste.DeRecursosDaFase()]).Value!;

    internal static FaseCronograma FaseDeColeta(int ordem) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []).Value!;

    internal static ProdutoDaFase Preliminar(string atoCodigo = ResultadoDaLista) => ProdutoDaFase.Criar(atoCodigo, PapelProdutoFase.Preliminar);

    internal static ProdutoDaFase Definitivo(string atoCodigo = ResultadoDaLista) => ProdutoDaFase.Criar(atoCodigo, PapelProdutoFase.Definitivo);

    internal static RegraRecursoFase JanelaDeRecurso(Guid produtoPreliminarId) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
        produtoPreliminarId).Value!;
}
