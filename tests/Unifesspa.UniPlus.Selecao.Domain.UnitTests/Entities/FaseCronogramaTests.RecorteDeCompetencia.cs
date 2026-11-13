namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class FaseCronogramaTests
{
    [Fact(DisplayName = "CA-03 (contraprova): dois atos DISTINTOS com o mesmo papel na mesma fase são aceitos")]
    public void Produtos_AtosDistintosComOMesmoPapel_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos:
            [
                ProdutoDaFase.Criar("GABARITO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ],
            faseConcluinteCodigo: "RESULTADO_FINAL");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Produtos.Should().HaveCount(2,
            "gabarito preliminar e resultado preliminar são documentos diferentes, e a fase publica os dois");
    }

    // ── CA-13/CA-14 — parecer individual ──

    [Fact(DisplayName = "CA-14: fase que promete parecer individual sem publicar resultado é recusada")]
    public void ParecerIndividual_SemResultado_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [ProdutoDaFase.Criar("COMUNICADO", null)],
            emiteParecerIndividual: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("FaseCronograma.ParecerIndividualSemResultado");
    }

    [Fact(DisplayName = "CA-14 (contraprova): fase que promete parecer individual e publica resultado é aceita")]
    public void ParecerIndividual_ComResultado_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            emiteParecerIndividual: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EmiteParecerIndividual.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-13 (fronteira): fase que NÃO promete parecer é aceita sem publicar resultado nenhum")]
    public void ParecerIndividual_NaoPrometido_SemResultado_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [],
            emiteParecerIndividual: false);

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
            .WithMessage("*não ancora em nenhum dos produtos preliminares*");
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

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [definitivo],
            regraRecurso: Recurso(definitivo.Id));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["RegraRecursoFase.FaseSemProdutoPreliminar"],
            "ancorar no definitivo é a mesma falta de sempre — a fase não publica decisão contestável");
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("produtos");
    }

    [Fact(DisplayName = "CA-03: fase que admite recurso e não publica produto nenhum é recusada pela ausência do preliminar")]
    public void RegraRecurso_SemProdutoAlgum_RecusaPelaAusenciaDoPreliminar()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [],
            regraRecurso: Recurso(Guid.CreateVersion7()));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["RegraRecursoFase.FaseSemProdutoPreliminar"],
            "a recusa que orienta manda declarar o preliminar, e não é diluída numa segunda sobre a âncora");
    }

    [Fact(DisplayName = "CA-01: a fase que publica preliminar E definitiva ancora o prazo no preliminar")]
    public void RegraRecurso_AncoraNoProdutoPreliminar_Aceita()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        RegraRecursoFase regraRecurso = Recurso(preliminar.Id);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [preliminar, ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
            regraRecurso: regraRecurso);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().Be(preliminar.Id,
            "a fase que conclui a própria matéria publica os dois resultados, e o prazo corre do preliminar");
    }

    [Fact(DisplayName = "CA-02: ancorar num produto que NÃO é preliminar desta fase é recusado")]
    public void RegraRecurso_AncoraForaDosPreliminares_Recusa()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [preliminar, definitivo],
            regraRecurso: Recurso(definitivo.Id));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should()
            .BeEquivalentTo(["RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase"]);
        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain("RESULTADO_PRELIMINAR");
    }

    [Fact(DisplayName = "CA-02: ancorar no produto de OUTRA fase que publica o MESMO tipo de ato é recusado")]
    public void RegraRecurso_AncoraNoProdutoHomonimoDeOutraFase_Recusa()
    {
        ProdutoDaFase preliminarDaOutraFase = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarDesta = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [preliminarDesta],
            regraRecurso: Recurso(preliminarDaOutraFase.Id));

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

        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            produtos: [gabarito, resultadoPreliminar],
            faseConcluinteCodigo: "RESULTADO_FINAL",
            regraRecurso: Recurso(resultadoPreliminar.Id));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().Be(resultadoPreliminar.Id);
        resultado.Value!.RegraRecurso!.ProdutoAncoraId.Should().NotBe(gabarito.Id);
    }

    [Fact(DisplayName = "Ordem menor ou igual a zero é recusada")]
    public void Ordem_MenorOuIgualAZero_Lanca()
    {
        Action act = () => Criar(ordem: 0);

        act.Should().Throw<ArgumentException>().WithParameterName("ordem");
    }

    [Fact(DisplayName = "ADR-0125: JanelaInvertida e ParecerIndividualSemResultado acumulam no mesmo lote")]
    public void Criar_JanelaInvertidaEParecerSemResultado_AcumulaAsDuasViolacoes()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            inicio: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            produtos: [],
            emiteParecerIndividual: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "FaseCronograma.JanelaInvertida",
            "FaseCronograma.ParecerIndividualSemResultado",
        ]);
        resultado.Errors.Should().Contain(e => e.Field == "fim" && e.Error.Code == "FaseCronograma.JanelaInvertida");
        resultado.Errors.Should().Contain(e => e.Field == "emiteParecerIndividual" && e.Error.Code == "FaseCronograma.ParecerIndividualSemResultado");
    }

    // ── CA-02 / CA-03 — recorte de competência das bancas requeridas ──

    private static BancaRequerida Banca(string codigo, params string[] categorias) =>
        BancaRequerida.Criar(
            Guid.CreateVersion7(),
            codigo,
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    [Fact(DisplayName = "CA-02: duas bancas do mesmo tipo, uma sem recorte, é recusada com erro nomeado")]
    public void RecorteDeCompetencia_DuasDoMesmoTipoUmaSemRecorte_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA"),
                Banca("BANCA_ANALISE_DOCUMENTAL"),
            ]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Error.Code == "BancaRequerida.RecorteDeCompetenciaObrigatorio"
                && e.Field == "bancasRequeridas[1].categoriasDocumentoIds");
    }

    [Fact(DisplayName = "CA-02: uma banca por tipo aceita recorte vazio — o tipo já a identifica")]
    public void RecorteDeCompetencia_UmaBancaPorTipoSemRecorte_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL"),
                Banca("BANCA_HETEROIDENTIFICACAO"),
            ]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-02: as duas bancas do mesmo tipo sem recorte acumulam uma recusa cada")]
    public void RecorteDeCompetencia_AmbasSemRecorte_AcumulaUmaPorBanca()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL"),
                Banca("BANCA_ANALISE_DOCUMENTAL"),
            ]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().AllBe("BancaRequerida.RecorteDeCompetenciaObrigatorio");
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["bancasRequeridas[0].categoriasDocumentoIds", "bancasRequeridas[1].categoriasDocumentoIds"],
            "a recusa que sai é a de recorte ausente, e não a de recorte duplicado — dois recortes vazios não são dois recortes iguais, são dois recortes que faltam");
    }

    [Fact(DisplayName = "CA-03: duas bancas do mesmo tipo com recortes distintos são declaráveis")]
    public void RecorteDeCompetencia_MesmoTipoRecortesDistintos_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA"),
                Banca("BANCA_ANALISE_DOCUMENTAL", "ETNICO_RACIAL"),
            ]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.BancasRequeridas.Should().HaveCount(2);
    }

    [Fact(DisplayName = "CA-03: duas bancas do mesmo tipo com o mesmo recorte declarado em outra ordem são recusadas")]
    public void RecorteDeCompetencia_MesmoTipoRecortesIguaisEmOutraOrdem_Recusa()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA", "ESCOLARIDADE"),
                Banca("BANCA_ANALISE_DOCUMENTAL", "ESCOLARIDADE", "RENDA"),
            ]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Error.Code == "BancaRequerida.RecorteDeCompetenciaDuplicado"
                && e.Field == "bancasRequeridas[1].categoriasDocumentoIds");
    }

    [Fact(DisplayName = "CA-03: recortes que se cruzam sem coincidir distinguem as bancas e são aceitos")]
    public void RecorteDeCompetencia_MesmoTipoRecortesQueSeCruzam_Aceita()
    {
        Result<FaseCronograma> resultado = Criar(
            origemData: OrigemDataFase.NaExecucao,
            bancasRequeridas:
            [
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA", "ESCOLARIDADE"),
                Banca("BANCA_ANALISE_DOCUMENTAL", "RENDA"),
            ]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
