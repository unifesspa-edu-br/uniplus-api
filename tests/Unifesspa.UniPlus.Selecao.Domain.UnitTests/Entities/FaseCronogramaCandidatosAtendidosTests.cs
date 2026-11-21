namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioFaseCronograma;

public sealed class FaseCronogramaCandidatosAtendidosTests
{
    [Fact(DisplayName = "CA-03 (contraprova): dois atos DISTINTOS com o mesmo papel na mesma fase são aceitos")]
    public void Produtos_AtosDistintosComOMesmoPapel_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Produtos = [
                    ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Produtos.Should().HaveCount(2,
            "gabarito preliminar e resultado preliminar são documentos diferentes, e a fase publica os dois");
    }

    // ── CA-13/CA-14 — parecer individual ──

    [Fact(DisplayName = "CA-14: fase que promete parecer individual sem publicar resultado é recusada")]
    public void ParecerIndividual_SemResultado_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [ProdutoDaFase.Criar("COMUNICADO", null)], EmiteParecerIndividual = true });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("FaseCronograma.ParecerIndividualSemResultado");
    }

    [Fact(DisplayName = "CA-14 (contraprova): fase que promete parecer individual e publica resultado é aceita")]
    public void ParecerIndividual_ComResultado_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual = true });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EmiteParecerIndividual.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-13 (fronteira): fase que NÃO promete parecer é aceita sem publicar resultado nenhum")]
    public void ParecerIndividual_NaoPrometido_SemResultado_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [], EmiteParecerIndividual = false });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EmiteParecerIndividual.Should().BeFalse();
    }

    [Fact(DisplayName = "Reidratar recusa âncora em produto que não é preliminar, como Criar e o decodificador recusam")]
    public void Reidratar_AncoraEmProdutoNaoPreliminar_Lanca()
    {
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo);
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);

        Action reidratar = () => Reidratar([preliminar, definitivo], Recurso(definitivo.Id));

        // A âncora PERTENCE à fase e mesmo assim é incoerente: conferir só a pertinência
        // deixaria a fase nascer com o prazo contando de um ato que encerra a matéria em vez
        // de abri-la — metade da invariante que Criar prova.
        reidratar.Should().Throw<ArgumentException>()
            .WithMessage("*não tem o prazo contado a partir de nenhum dos produtos preliminares*");
    }

    [Fact(DisplayName = "Reidratar recusa âncora em produto de outra fase")]
    public void Reidratar_AncoraForaDaFase_Lanca()
    {
        ProdutoDaFase preliminarDaOutraFase = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarDesta = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);

        Action reidratar = () => Reidratar([preliminarDesta], Recurso(preliminarDaOutraFase.Id));

        reidratar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Contraprova: Reidratar aceita a âncora no produto preliminar da própria fase")]
    public void Reidratar_AncoraNoPreliminarDaPropriaFase_Aceita()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);

        FaseCronograma fase = Reidratar([preliminar], Recurso(preliminar.Id));

        fase.RegraRecurso!.ProdutoAncoraId.Should().Be(preliminar.Id);
    }

    // ── CA-01/CA-02/CA-03 — a âncora do prazo é um produto preliminar da própria fase ──

    [Fact(DisplayName = "CA-03: fase que admite recurso e não publica produto preliminar é recusada")]
    public void RegraRecurso_SemProdutoPreliminar_Recusa()
    {
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo);

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [definitivo], RegraRecurso = Recurso(definitivo.Id) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["RegraRecursoFase.FaseSemProdutoPreliminar"],
            "ancorar no definitivo é a mesma falta de sempre — a fase não publica decisão contestável");
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("produtos");
    }

    [Fact(DisplayName = "CA-03: fase que admite recurso e não publica produto nenhum é recusada pela ausência do preliminar")]
    public void RegraRecurso_SemProdutoAlgum_RecusaPelaAusenciaDoPreliminar()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [], RegraRecurso = Recurso(Guid.CreateVersion7()) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["RegraRecursoFase.FaseSemProdutoPreliminar"],
            "a recusa que orienta manda declarar o preliminar, e não é diluída numa segunda sobre a âncora");
    }

    [Fact(DisplayName = "CA-01: a fase que publica preliminar E definitiva ancora o prazo no preliminar")]
    public void RegraRecurso_AncoraNoProdutoPreliminar_Aceita()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        RegraRecursoFase regraRecurso = Recurso(preliminar.Id);

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [preliminar, ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], RegraRecurso = regraRecurso });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().Be(preliminar.Id,
            "a fase que conclui a própria matéria publica os dois resultados, e o prazo corre do preliminar");
    }

    [Fact(DisplayName = "CA-02: ancorar num produto que NÃO é preliminar desta fase é recusado")]
    public void RegraRecurso_AncoraForaDosPreliminares_Recusa()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo);

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [preliminar, definitivo], RegraRecurso = Recurso(definitivo.Id) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should()
            .BeEquivalentTo(["RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase"]);
        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain("RESULTADO_CLASSIFICACAO");
    }

    [Fact(DisplayName = "CA-02: ancorar no produto de OUTRA fase que publica o MESMO tipo de ato é recusado")]
    public void RegraRecurso_AncoraNoProdutoHomonimoDeOutraFase_Recusa()
    {
        ProdutoDaFase preliminarDaOutraFase = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarDesta = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [preliminarDesta], RegraRecurso = Recurso(preliminarDaOutraFase.Id) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should()
            .BeEquivalentTo(["RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase"],
                "o código do tipo de ato coincide nas duas fases, e é a identidade do produto que as distingue");
    }

    [Fact(DisplayName = "CA-01: a fase que publica DOIS preliminares ancora no que declarou, não no outro")]
    public void RegraRecurso_DoisPreliminares_AncoraNoDeclarado()
    {
        ProdutoDaFase gabarito = ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase resultadoPreliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);

        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Produtos = [gabarito, resultadoPreliminar], RegraRecurso = Recurso(resultadoPreliminar.Id) });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().Be(resultadoPreliminar.Id);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().NotBe(gabarito.Id);
    }

    [Fact(DisplayName = "Ordem menor ou igual a zero é recusada")]
    public void Ordem_MenorOuIgualAZero_Lanca()
    {
        Action act = () => Criar(m => m with { Ordem = 0 });

        act.Should().Throw<ArgumentException>().WithParameterName("ordem");
    }

    [Fact(DisplayName = "ADR-0125: JanelaInvertida e ParecerIndividualSemResultado acumulam no mesmo lote")]
    public void Criar_JanelaInvertidaEParecerSemResultado_AcumulaAsDuasViolacoes()
    {
        Result<FaseCronograma> resultado = Criar(m => m with { OrigemData = OrigemDataFase.NaExecucao, Inicio = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Fim = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Produtos = [], EmiteParecerIndividual = true });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "FaseCronograma.JanelaInvertida",
            "FaseCronograma.ParecerIndividualSemResultado",
        ]);
        resultado.Errors.Should().Contain(e => e.Field == "fim" && e.Error.Code == "FaseCronograma.JanelaInvertida");
        resultado.Errors.Should().Contain(e => e.Field == "emiteParecerIndividual" && e.Error.Code == "FaseCronograma.ParecerIndividualSemResultado");
    }

    // ── CA-02 / CA-03 — candidatos atendidos das bancas requeridas ──

    [Fact(DisplayName = "CA-02: duas bancas do mesmo tipo, uma sem candidatos atendidos, é recusada com erro nomeado")]
    public void CandidatosAtendidos_DuasDoMesmoTipoUmaSemCandidatosAtendidos_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca(TipoDeBanca.AnaliseDocumental, "RENDA"),
                    Banca(TipoDeBanca.AnaliseDocumental),
                ],
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Error.Code == "BancaRequerida.CandidatosAtendidosObrigatorios"
                && e.Field == "bancasRequeridas[1].categoriasDocumentoIds");
    }

    [Fact(DisplayName = "CA-02: uma banca por tipo aceita candidatos atendidos vazios — o tipo já a identifica")]
    public void CandidatosAtendidos_UmaBancaPorTipoSemCandidatosAtendidos_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca(TipoDeBanca.AnaliseDocumental),
                    Banca(TipoDeBanca.Heteroidentificacao),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-02: as bancas do mesmo tipo sem candidatos atendidos atendem todos os candidatos e são aceitas")]
    public void CandidatosAtendidos_AmbasSemCandidatosAtendidos_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca(TipoDeBanca.AnaliseDocumental),
                    Banca(TipoDeBanca.AnaliseDocumental),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.BancasRequeridas.Select(banca => banca.PosicaoNoTipo).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "CA-02: cada banca sem candidatos atendidos ao lado de banca com candidatos atendidos acumula uma recusa")]
    public void CandidatosAtendidos_VariasSemCandidatosAtendidosAoLadoDeUmaComCandidatosAtendidos_AcumulaUmaPorBanca()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca(TipoDeBanca.AnaliseDocumental),
                    Banca(TipoDeBanca.AnaliseDocumental, "RENDA"),
                    Banca(TipoDeBanca.AnaliseDocumental),
                ],
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().AllBe("BancaRequerida.CandidatosAtendidosObrigatorios");
        resultado.Errors.Select(e => e.Field).Should().Equal(
            "bancasRequeridas[0].categoriasDocumentoIds", "bancasRequeridas[2].categoriasDocumentoIds");
    }

    [Fact(DisplayName = "CA-03: duas bancas do mesmo tipo com candidatos atendidos distintos são declaráveis")]
    public void CandidatosAtendidos_MesmoTipoCandidatosAtendidosDistintos_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca(TipoDeBanca.AnaliseDocumental, "RENDA"),
                    Banca(TipoDeBanca.AnaliseDocumental, "ETNICO_RACIAL"),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.BancasRequeridas.Should().HaveCount(2);
    }

    [Fact(DisplayName = "CA-03: duas bancas do mesmo tipo com os mesmos candidatos atendidos declarados em outra ordem julgam em paralelo")]
    public void CandidatosAtendidos_MesmoTipoCandidatosAtendidosIguaisEmOutraOrdem_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca(TipoDeBanca.AnaliseDocumental, "RENDA", "ESCOLARIDADE"),
                    Banca(TipoDeBanca.AnaliseDocumental, "ESCOLARIDADE", "RENDA"),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-03: candidatos atendidos que se cruzam sem coincidir distinguem as bancas e são aceitos")]
    public void CandidatosAtendidos_MesmoTipoCandidatosAtendidosQueSeCruzam_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(m => m with
        {
            OrigemData = OrigemDataFase.NaExecucao,
            Bancas = [
                    Banca(TipoDeBanca.AnaliseDocumental, "RENDA", "ESCOLARIDADE"),
                    Banca(TipoDeBanca.AnaliseDocumental, "RENDA"),
                ],
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
