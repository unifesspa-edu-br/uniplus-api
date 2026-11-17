namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.FasesDeHomologacaoParaTestes;

/// <summary>
/// A regra que a fase declara, conferida contra a própria fase e o cronograma: o tipo de decisão declarado,
/// o resultado que leva a lista e a confirmação do INEP quando o processo importa a nota de um exame.
/// </summary>
public sealed class ConferenciaDaRegraDeHomologacaoTests
{
    private static readonly string[] TipoDaHomologacao = [TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes];

    private static RegraDeHomologacaoDasInscricoes Regra(string? resultado = ResultadoDaLista, params AlternativaDeCondicoes[] alternativas) =>
        RegraDeHomologacaoDasInscricoes.Declarar(resultado, alternativas);

    private static FaseCronograma FaseComListaFechada() => FaseQueHomologa(1, "HOMOLOGACAO_INSCRICOES", [Preliminar(), Definitivo()]);

    private static FaseCronograma FaseQueHomologa(int ordem, string codigo, IReadOnlyList<ProdutoDaFase>? produtos = null, string? faseConcluinteCodigo = null) =>
        Fase(ordem, codigo, produtos, faseConcluinteCodigo: faseConcluinteCodigo, tiposDeDecisao: TipoDaHomologacao);

    private static IReadOnlyList<FieldError> Faltas(
        RegraDeHomologacaoDasInscricoes regra, FaseCronograma fase, bool exame = false, params FaseCronograma[] outras) =>
        ConferenciaDaRegraDeHomologacao.Faltas(regra, fase, [fase, .. outras], exame);

    [Fact(DisplayName = "Regra com o tipo declarado, o resultado na fase e o definitivo na própria fase não tem falta")]
    public void Regra_Completa_SemFalta()
    {
        FaseCronograma fase = FaseComListaFechada();

        Faltas(Regra(), fase).Should().BeEmpty();
    }

    [Theory(DisplayName = "Regra numa fase que não declara o tipo HOMOLOGACAO_DAS_INSCRICOES é recusada no campo da regra, qualquer que seja o código da fase")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    [InlineData("CLASSIFICACAO")]
    public void Regra_FaseSemOTipo_Recusa(string codigo)
    {
        FaseCronograma fase = Fase(1, codigo, [Preliminar(), Definitivo()], tiposDeDecisao: [TipoMotivoDeDecisaoCodigo.Classificacao]);

        IReadOnlyList<FieldError> faltas = Faltas(Regra(), fase);

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("regraDeHomologacao");
        faltas[0].Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
    }

    [Fact(DisplayName = "Resultado apontado que a fase não publica é recusado no campo do resultado")]
    public void Resultado_ForaDaFase_Recusa()
    {
        FaseCronograma fase = FaseComListaFechada();

        IReadOnlyList<FieldError> faltas = Faltas(Regra("ATO_SENTINELA"), fase);

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("regraDeHomologacao.resultadoAtoCodigo");
        faltas[0].Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
        faltas[0].Error.Message.Should().Be("O resultado indicado pela regra deve ser publicado nesta fase, com resultado definitivo nela ou na fase que a conclui.");
    }

    [Fact(DisplayName = "Regra que não aponta o resultado é recusada como resultado não declarado")]
    public void Resultado_NaoApontado_Recusa()
    {
        FaseCronograma fase = FaseComListaFechada();

        IReadOnlyList<FieldError> faltas = Faltas(Regra(resultado: null), fase);

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("regraDeHomologacao.resultadoAtoCodigo");
        faltas[0].Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "Fase sem nenhum resultado publicado recusa a regra como resultado não declarado")]
    public void Resultado_FaseSemProdutos_Recusa()
    {
        FaseCronograma fase = FaseQueHomologa(1, "HOMOLOGACAO_INSCRICOES");

        Faltas(Regra(), fase).Should().ContainSingle()
            .Which.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "Fase com mais de um resultado aceita a regra: só o apontado leva a lista")]
    public void Resultado_FaseComVariosResultados_Aceita()
    {
        FaseCronograma fase = FaseQueHomologa(1, "HOMOLOGACAO_INSCRICOES", [Preliminar(), Definitivo(), Preliminar("OUTRO_RESULTADO"), Definitivo("OUTRO_RESULTADO")]);

        Faltas(Regra("OUTRO_RESULTADO"), fase).Should().BeEmpty();
    }

    [Fact(DisplayName = "Resultado apontado só com preliminar, sem definitivo na fase nem em fase concluinte, é recusado")]
    public void Resultado_SemDefinitivo_Recusa()
    {
        FaseCronograma fase = FaseQueHomologa(1, "HOMOLOGACAO_INSCRICOES", [Preliminar()]);

        Faltas(Regra(), fase).Should().ContainSingle()
            .Which.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "O definitivo publicado pela fase concluinte do cronograma basta")]
    public void Resultado_DefinitivoNaFaseConcluinte_Aceita()
    {
        FaseCronograma fase = FaseConcluidaPor("RESULTADO_FINAL", [Preliminar()]);
        FaseCronograma concluinte = Fase(2, "RESULTADO_FINAL", [Definitivo()]);

        Faltas(Regra(), fase, exame: false, concluinte).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase concluinte que publica o definitivo de outro resultado não basta")]
    public void Resultado_ConcluinteComDefinitivoDeOutroResultado_Recusa()
    {
        FaseCronograma fase = FaseConcluidaPor("RESULTADO_FINAL", [Preliminar()]);
        FaseCronograma concluinte = Fase(2, "RESULTADO_FINAL", [Definitivo("OUTRO_RESULTADO")]);

        Faltas(Regra(), fase, exame: false, concluinte).Should().ContainSingle()
            .Which.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "O definitivo publicado por outra fase que não é a concluinte declarada não basta")]
    public void Resultado_DefinitivoEmFaseQueNaoEhAConcluinte_Recusa()
    {
        FaseCronograma fase = FaseConcluidaPor("RESULTADO_FINAL", [Preliminar()]);
        FaseCronograma concluinte = Fase(2, "RESULTADO_FINAL", [Preliminar()]);
        FaseCronograma outra = Fase(3, "OUTRA_FASE", [Definitivo()]);

        Faltas(Regra(), fase, exame: false, concluinte, outra).Should().ContainSingle()
            .Which.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "Importação de exame com regra vazia é recusada: falta a confirmação do INEP entre as condições")]
    public void Exame_RegraVazia_FaltaConfirmacaoDoInep()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Regra(), FaseComListaFechada(), exame: true);

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("regraDeHomologacao.alternativas");
        faltas[0].Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente);
    }

    [Fact(DisplayName = "Importação de exame com a confirmação do INEP em todas as alternativas não tem falta")]
    public void Exame_ConfirmacaoEmTodasAsAlternativas_SemFalta()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(ResultadoDaLista,
            Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP")),
            Alternativa(ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP_ANTERIOR")));

        Faltas(regra, FaseComListaFechada(), exame: true).Should().BeEmpty();
    }

    [Fact(DisplayName = "A confirmação do INEP restrita por enquadramento não alcança todos os candidatos e não conta")]
    public void Exame_ConfirmacaoComEnquadramento_FaltaConfirmacaoDoInep()
    {
        RegraDeHomologacaoDasInscricoes regra = Regra(ResultadoDaLista,
            Alternativa(ConfirmacaoDoInepSoParaAmpla(2026, "SEM_RESULTADO_INEP")));

        Faltas(regra, FaseComListaFechada(), exame: true).Should().ContainSingle()
            .Which.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente);
    }

    [Fact(DisplayName = "Sem nota importada de exame, a regra vazia é aceita pela conferência (o aviso é à parte)")]
    public void SemExame_RegraVazia_SemFalta()
    {
        Faltas(Regra(), FaseComListaFechada(), exame: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "As faltas se acumulam na ordem tipo, resultado e confirmação do INEP")]
    public void Faltas_AcumulamNaOrdemDaConferencia()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Regra("OUTRO_RESULTADO"), Fase(1, "HOMOLOGACAO_INSCRICOES", [Preliminar(), Definitivo()]), exame: true);

        faltas.Select(f => f.Error.Code).Should().Equal(
            MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado,
            HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado,
            HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente);
    }

    private static FaseCronograma FaseConcluidaPor(string concluinte, IReadOnlyList<ProdutoDaFase> produtos) =>
        FaseQueHomologa(1, "HOMOLOGACAO_INSCRICOES", produtos, concluinte);
}
