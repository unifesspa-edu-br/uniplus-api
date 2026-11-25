namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using System.Collections.Frozen;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.FasesDeHomologacaoParaTestes;

/// <summary>
/// O resultado que a regra de homologação aponta é conferido contra os produtos, com papel, da própria fase. O definitivo
/// dele é do ciclo recursal, e a falta do tipo de decisão e da confirmação do INEP é de outras regras.
/// </summary>
public sealed class ConferenciaDaRegraDeHomologacaoTests
{
    private const string FaseDaRegra = "HOMOLOGACAO_INSCRICOES";
    private const string CampoDoResultado = "regraDeHomologacao.resultadoAtoCodigo";

    private static RegraDeHomologacaoDasInscricoes Regra(string? resultado = ResultadoDaLista) =>
        RegraDeHomologacaoDasInscricoes.Declarar(resultado, []);

    private static PublicacoesDaFase Publicando(RegraDeHomologacaoDasInscricoes? regra, params ProdutoDaFase[] produtos) =>
        new(produtos, EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: null, RegraDeHomologacao: regra);

    private static FieldError? Ausente(RegraDeHomologacaoDasInscricoes? regra, string codigoDaFase = FaseDaRegra) =>
        ConferenciaDaRegraDeHomologacao.ResultadoAusente(codigoDaFase, regra);

    private static FieldError? QueNaoEProduto(
        RegraDeHomologacaoDasInscricoes? regra, ProdutoDaFase[] produtos, string codigoDaFase = FaseDaRegra) =>
        ConferenciaDaRegraDeHomologacao.ResultadoQueNaoEProduto(codigoDaFase, Publicando(regra, produtos));

    [Fact(DisplayName = "O resultado apontado que é produto com papel da fase não tem recusa")]
    public void Resultado_ProdutoComPapel_SemRecusa() =>
        QueNaoEProduto(Regra(), [Preliminar()]).Should().BeNull();

    [Fact(DisplayName = "A fase sem regra não tem o que conferir")]
    public void SemRegra_SemRecusa()
    {
        Ausente(regra: null).Should().BeNull();
        QueNaoEProduto(regra: null, []).Should().BeNull();
    }

    [Fact(DisplayName = "Regra que não aponta o resultado é recusada no campo do resultado")]
    public void Resultado_NaoApontado_Recusa()
    {
        FieldError? recusa = Ausente(Regra(resultado: null));

        recusa.Should().NotBeNull();
        recusa!.Field.Should().Be(CampoDoResultado);
        recusa.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
        recusa.Error.Message.Should().Contain(FaseDaRegra);
    }

    [Theory(DisplayName = "Resultado em branco vale como não apontado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Resultado_EmBranco_Recusa(string resultado) =>
        Ausente(Regra(resultado)).Should().NotBeNull();

    [Fact(DisplayName = "Regra que aponta o resultado não é recusada como resultado ausente, qualquer que seja ele")]
    public void Resultado_Apontado_NaoEAusente() =>
        Ausente(Regra("OUTRO_RESULTADO")).Should().BeNull();

    [Fact(DisplayName = "Resultado apontado que a fase não publica é recusado no campo do resultado")]
    public void Resultado_QueNaoEProdutoDaFase_Recusa()
    {
        FieldError? recusa = QueNaoEProduto(Regra("OUTRO_RESULTADO"), [Preliminar(), Definitivo()]);

        recusa.Should().NotBeNull();
        recusa!.Field.Should().Be(CampoDoResultado);
        recusa.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
        recusa.Error.Message.Should().Contain("precisa ser produto, com papel").And.Contain(FaseDaRegra).And.NotContain("OUTRO_RESULTADO");
    }

    [Theory]
    [InlineData(" RESULTADO_HOMOLOGACAO ")]
    [InlineData("resultado_homologacao")]
    public void Resultado_CodigoInexato_Recusa(string codigo)
    {
        FieldError? recusa = QueNaoEProduto(Regra(codigo), [Preliminar("RESULTADO_HOMOLOGACAO")]);

        recusa!.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
        recusa.Error.Message.Should().NotContain(codigo);
    }

    [Fact(DisplayName = "Regra sem resultado não é recusada como resultado que não é produto: a falta é de outra regra")]
    public void Resultado_NaoApontado_NaoEQueNaoEProduto() =>
        QueNaoEProduto(Regra(resultado: null), [Preliminar()]).Should().BeNull();

    [Fact(DisplayName = "Produto sem papel, que é só um ato publicado pela fase, não é o resultado da regra")]
    public void Resultado_ProdutoSemPapel_Recusa()
    {
        FieldError? recusa = QueNaoEProduto(Regra(), [ProdutoDaFase.Criar(ResultadoDaLista, papel: null)]);

        recusa.Should().NotBeNull();
        recusa!.Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "Fase sem nenhum produto recusa a regra como resultado não declarado")]
    public void Resultado_FaseSemProdutos_Recusa() =>
        QueNaoEProduto(Regra(), []).Should().NotBeNull();

    [Fact(DisplayName = "Fase com mais de um resultado aceita a regra: só o apontado leva a lista")]
    public void Resultado_FaseComVariosResultados_Aceita() =>
        QueNaoEProduto(Regra("OUTRO_RESULTADO"), [Preliminar(), Definitivo(), Preliminar("OUTRO_RESULTADO"), Definitivo("OUTRO_RESULTADO")])
            .Should().BeNull();

    [Fact(DisplayName = "O definitivo do resultado não é conferido por esta regra: só o preliminar basta")]
    public void Resultado_SemDefinitivo_NaoEConferidoAqui() =>
        QueNaoEProduto(Regra(), [Preliminar()]).Should().BeNull();

    [Theory(DisplayName = "O código da fase não decide a conferência: o resultado vem dos produtos qualquer que seja o código")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    [InlineData("HOMOLOGACAO_RESULTADO_FINAL")]
    [InlineData("CLASSIFICACAO")]
    public void Resultado_QualquerCodigoDeFase_ConfereOsProdutos(string codigoDaFase)
    {
        QueNaoEProduto(Regra(), [Preliminar()], codigoDaFase).Should().BeNull();
        QueNaoEProduto(Regra("OUTRO_RESULTADO"), [Preliminar()], codigoDaFase).Should().NotBeNull();
    }

    [Fact(DisplayName = "Criar a fase recusa o resultado que a regra aponta e não é produto, no campo da fase")]
    public void Criar_ResultadoQueNaoEProduto_RecusaNoCampoDaRegra()
    {
        Result<FaseCronograma> fase = FaseComRegra(Regra("OUTRO_RESULTADO"), [Preliminar()]);

        fase.IsFailure.Should().BeTrue();
        fase.Errors.Should().ContainSingle();
        fase.Errors[0].Field.Should().Be(CampoDoResultado);
        fase.Errors[0].Error.Code.Should().Be(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "A recusa do resultado vem depois da dos tipos de decisão declarados")]
    public void Criar_TipoDeDecisaoInvalidoEResultadoAusente_RecusaNaOrdemDaDeclaracao()
    {
        Result<FaseCronograma> fase = FaseComRegra(Regra(resultado: null), [Preliminar()], "TIPO_FORA_DO_VOCABULARIO");

        fase.Errors.Select(static e => e.Error.Code).Should().Equal(
            MotivoDeDecisaoErrorCodes.TipoInvalido,
            HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "A fase declarada com a lista de produtos recusada na leitura não julga o resultado apontado")]
    public void Declarar_ProdutosComParteRecusada_NaoJulgaOResultado()
    {
        FaseDeclarada fase = Declarar(Regra("OUTRO_RESULTADO"), [Preliminar()], new HashSet<string> { FaseDeclarada.ListaDosProdutos });

        fase.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase declarada com a lista de produtos recusada na leitura recusa a regra sem resultado")]
    public void Declarar_ProdutosComParteRecusadaESemResultado_RecusaOResultadoAusente()
    {
        FaseDeclarada fase = Declarar(Regra(resultado: null), [], new HashSet<string> { FaseDeclarada.ListaDosProdutos });

        fase.Recusas.Should().ContainSingle().Which.Field.Should().Be(CampoDoResultado);
    }

    [Fact(DisplayName = "A fase declarada sem parte recusada recusa o resultado apontado que não é produto")]
    public void Declarar_ResultadoQueNaoEProduto_Recusa()
    {
        FaseDeclarada fase = Declarar(Regra("OUTRO_RESULTADO"), [Preliminar()], FrozenSet<string>.Empty);

        fase.Recusas.Should().ContainSingle().Which.Field.Should().Be(CampoDoResultado);
    }

    private static Result<FaseCronograma> FaseComRegra(
        RegraDeHomologacaoDasInscricoes regra, ProdutoDaFase[] produtos, params string?[] tiposDeDecisao) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!, Origem(), Decisoes(), new PublicacoesDaFase(produtos, false, null, tiposDeDecisao, regra), []);

    private static FaseDeclarada Declarar(RegraDeHomologacaoDasInscricoes regra, ProdutoDaFase[] produtos, IReadOnlySet<string> colecoesRecusadas) =>
        FaseDeclarada.Declarar(Guid.CreateVersion7(), Origem(), Decisoes(), Publicando(regra, produtos), [], colecoesRecusadas);

    private static OrigemCanonicaDaFase Origem() => OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
        Guid.CreateVersion7(), FaseDaRegra, OrigemDataFase.NaExecucao,
        AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false));

    private static DecisoesDeCronogramaDaFase Decisoes() => new(1, null, null, UnidadesDeTeste.Ceps);
}
