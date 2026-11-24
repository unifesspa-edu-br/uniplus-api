namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A homologação das inscrições que o processo publicável de teste declara na fase que publica o resultado: o tipo de
/// decisão, e a regra que aponta o resultado da fase e confirma o retorno do INEP, com o motivo desfavorável da condição.
/// </summary>
internal static class HomologacaoDasInscricoesDeTeste
{
    internal const string ResultadoDaFase = "RESULTADO_CLASSIFICACAO";
    internal const string MotivoSemResultadoNoInep = "SEM_RESULTADO_INEP";
    internal const string CodigoDaConfirmacaoDoInep = "RESULTADO_NO_RETORNO_DO_INEP";

    /// <summary>O tipo de decisão que a fase que homologa declara produzir.</summary>
    internal static IReadOnlyList<string?> TiposDeDecisao { get; } = [TipoMotivoDeDecisao.HomologacaoDasInscricoes.Codigo()];

    /// <summary>A regra que aponta o resultado e homologa a inscrição que tem resultado no retorno do INEP.</summary>
    internal static RegraDeHomologacaoDasInscricoes Regra(string resultadoAtoCodigo = ResultadoDaFase) =>
        RegraDeHomologacaoDasInscricoes.Declarar(resultadoAtoCodigo, [Alternativa(ConfirmacaoDoInep())]);

    /// <summary>
    /// As publicações da fase que publica o resultado definitivo e homologa as inscrições: o tipo de decisão e a regra que aponta
    /// o resultado dela.
    /// </summary>
    internal static PublicacoesDaFase PublicacoesDaFaseQueHomologa(
        IReadOnlyList<ProdutoDaFase>? produtos = null, RegraRecursoFase? regraRecurso = null, string resultado = ResultadoDaFase) => new(
            produtos ?? [ProdutoDaFase.Criar(resultado, PapelDaPublicacaoPrevista.Definitivo)],
            EmiteParecerIndividual: false,
            regraRecurso,
            TiposDeDecisao,
            Regra(resultado));

    /// <summary>
    /// As publicações da fase que homologa as inscrições de quem importa os candidatos: o resultado preliminar e o definitivo, com a
    /// janela de recurso ancorada no preliminar e o procedimento da decisão de homologação.
    /// </summary>
    internal static PublicacoesDaFase PublicacoesDaFaseQueHomologaComRecurso(string resultado = ResultadoDaFase)
    {
        Guid preliminarId = Guid.CreateVersion7();

        return PublicacoesDaFaseQueHomologa(
            [
                ProdutoDaFase.Reidratar(preliminarId, resultado, PapelDaPublicacaoPrevista.Preliminar),
                ProdutoDaFase.Criar(resultado, PapelDaPublicacaoPrevista.Definitivo),
            ],
            RegraDeRecurso(preliminarId),
            resultado);
    }

    /// <summary>A fase que homologa as inscrições de quem importa os candidatos, com o ciclo de recurso completo, na ordem dada.</summary>
    internal static FaseCronograma FaseQueHomologaComRecurso(int ordem, string codigo = "HOMOLOGACAO_DAS_INSCRICOES", string resultado = "RESULTADO_DA_HOMOLOGACAO") => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        PublicacoesDaFaseQueHomologaComRecurso(resultado),
        bancasRequeridas: BancasDaFaseQueHomologaComRecurso()).Value!;

    /// <summary>
    /// As bancas da fase que homologa as inscrições de quem importa os candidatos: a de homologação das inscrições, a de importação
    /// de candidatos e a que julga o recurso.
    /// </summary>
    internal static IReadOnlyList<BancaRequerida> BancasDaFaseQueHomologaComRecurso() =>
        [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), BancasDeTeste.DaFase(TipoDeBanca.ImportacaoDeCandidatos), BancasDeTeste.DeRecursosDaFase()];

    /// <summary>A janela de recurso ancorada no produto dado, com o procedimento da decisão de homologação.</summary>
    internal static RegraRecursoFase RegraDeRecurso(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('d', 64)),
        ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.DiasUteis),
        produtoAncoraId,
        ProcedimentosDaHomologacao()).Value!;

    internal static ProcedimentosDeRecurso ProcedimentosDaHomologacao() => ProcedimentosDeRecurso.De(
        [ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.HomologacaoDasInscricoes,
            alvo: null,
            ReferenciaRegra.De(RegraDeProcedimentoDoRecurso.Recorrecao.Codigo(), "v1", new string('a', 64)))]);

    internal static CondicaoDeHomologacao ConfirmacaoDoInep(string motivo = MotivoSemResultadoNoInep) =>
        CondicaoDeHomologacao.Declarar(CodigoDaConfirmacaoDoInep, edicaoDoExame: 2026, motivo, enquadramento: null).Value!;

    internal static AlternativaDeCondicoes Alternativa(params CondicaoDeHomologacao[] condicoes) =>
        AlternativaDeCondicoes.Declarar(condicoes).Value!;
}
