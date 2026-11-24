namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CriterioDesempateTests
{
    private static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.De(codigo, "v1", new string('a', 64));

    private static ReferenciaRegra Regra(RegraDeDesempate regra) => Regra(regra.Codigo());

    private static CondicaoDnf CondicaoProfessorRural() =>
        CondicaoDnf.Criar("PROFESSOR_RURAL", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

    [Fact]
    public void Criar_RegraDesconhecida_RecusaParametrosIncompativeis()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra("DESEMPATE-INEXISTENTE"), new ParametrosDesempateMaiorIdade());
        resultado.Error!.Code.Should().Be("CriterioDesempate.ParametrosIncompativeisComRegra");
    }

    [Fact(DisplayName = "Criar DESEMPATE_MAIOR_NOTA_ETAPA com parâmetros compatíveis tem sucesso")]
    public void Criar_MaiorNotaEtapa_Sucesso()
    {
        Guid etapaId = Guid.CreateVersion7();
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaNaEtapa), new ParametrosDesempateMaiorNotaEtapa(etapaId));

        resultado.IsSuccess.Should().BeTrue();
        ((ParametrosDesempateMaiorNotaEtapa)resultado.Value!.Parametros).EtapaRef.Should().Be(etapaId);
    }

    [Fact(DisplayName = "Criar DESEMPATE_MAIOR_IDADE (sem parâmetros) tem sucesso")]
    public void Criar_MaiorIdade_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorIdade), new ParametrosDesempateMaiorIdade());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar DESEMPATE_IDOSO com idade mínima válida tem sucesso")]
    public void Criar_Idoso_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.Idoso), new ParametrosDesempateIdoso(60));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar DESEMPATE_CONDICAO_SOBRE_DADO com parâmetros completos tem sucesso")]
    public void Criar_PredicadoFato_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.CondicaoSobreDado), new ParametrosDesempatePredicadoFato(CondicaoProfessorRural()));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar DESEMPATE_CONDICAO_SOBRE_DADO com fato fora do vocabulário fechado falha")]
    public void Criar_PredicadoFato_FatoDesconhecido_Falha()
    {
        Dictionary<string, DescritorFatoCandidato> vocabulario = new()
        {
            ["PROFESSOR_RURAL"] = DescritorFatoCandidato.Criar("PROFESSOR_RURAL", TipoDominioFato.Booleano, null).Value!,
        };

        CondicaoDnf condicaoDesconhecida = CondicaoDnf.Criar("FATO_INEXISTENTE", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.CondicaoSobreDado), new ParametrosDesempatePredicadoFato(condicaoDesconhecida), vocabulario);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Theory(DisplayName = "A recusa do predicado sai no campo que a corrige: fato, operador ou valor")]
    [InlineData("FATO_INEXISTENTE", Operador.Igual, "true", "fato")]
    [InlineData("PROFESSOR_RURAL", Operador.MaiorIgual, "true", "operador")]
    [InlineData("PROFESSOR_RURAL", Operador.Igual, "\"sim\"", "valor")]
    public void Criar_PredicadoRecusado_NoCampoQueCorrige(string fato, Operador operador, string valor, string campo)
    {
        Dictionary<string, DescritorFatoCandidato> vocabulario = new()
        {
            ["PROFESSOR_RURAL"] = DescritorFatoCandidato.Criar("PROFESSOR_RURAL", TipoDominioFato.Booleano, null).Value!,
        };
        CondicaoDnf condicao = CondicaoDnf.Criar(fato, operador, JsonDocument.Parse(valor).RootElement.Clone()).Value!;

        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.CondicaoSobreDado), new ParametrosDesempatePredicadoFato(condicao), vocabulario);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be(campo);
    }

    [Fact(DisplayName = "Criar DESEMPATE_CONDICAO_SOBRE_DADO com fato do vocabulário fechado tem sucesso")]
    public void Criar_PredicadoFato_FatoConhecido_Sucesso()
    {
        Dictionary<string, DescritorFatoCandidato> vocabulario = new()
        {
            ["PROFESSOR_RURAL"] = DescritorFatoCandidato.Criar("PROFESSOR_RURAL", TipoDominioFato.Booleano, null).Value!,
        };

        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.CondicaoSobreDado), new ParametrosDesempatePredicadoFato(CondicaoProfessorRural()), vocabulario);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com ordem não positiva falha")]
    public void Criar_OrdemInvalida_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            0, Regra(RegraDeDesempate.MaiorIdade), new ParametrosDesempateMaiorIdade());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.OrdemInvalida");
    }

    [Fact(DisplayName = "Criar com parâmetros incompatíveis com a regra falha")]
    public void Criar_ParametrosIncompativeis_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaNaEtapa), new ParametrosDesempateMaiorIdade());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.ParametrosIncompativeisComRegra");
    }

    [Fact(DisplayName = "Criar DESEMPATE_IDOSO com idade mínima não positiva falha")]
    public void Criar_IdadeMinimaInvalida_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.Idoso), new ParametrosDesempateIdoso(0));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.IdadeMinimaInvalida");
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida e idade mínima inválida acumulam no mesmo lote")]
    public void Criar_OrdemInvalidaEIdadeMinimaInvalida_AcumulaAsDuasViolacoes()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            0, Regra(RegraDeDesempate.Idoso), new ParametrosDesempateIdoso(0));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "CriterioDesempate.OrdemInvalida",
            "CriterioDesempate.IdadeMinimaInvalida",
        ]);
    }

    [Fact(DisplayName = "Criar DESEMPATE_MAIOR_NOTA_AREA_ENEM guarda os códigos na ordem declarada")]
    public void Criar_MaiorNotaAreaEnem_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO", "MATEMATICA"]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ((ParametrosDesempateMaiorNotaAreaEnem)resultado.Value!.Parametros).Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Mudar a lista entregue depois da validação não altera a ordem de áreas do critério")]
    public void Criar_MaiorNotaAreaEnem_ListaDeOrigemMutadaDepois_NaoAlteraOCriterio()
    {
        string[] areas = ["REDACAO", "MATEMATICA"];
        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new ParametrosDesempateMaiorNotaAreaEnem(areas)).Value!;

        areas[0] = "redacao";
        areas[1] = "REDACAO";

        ((ParametrosDesempateMaiorNotaAreaEnem)criterio.Parametros).Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Parametros de área do ENEM são iguais pelo conteúdo e pela ordem, como as variantes escalares")]
    public void ParametrosMaiorNotaAreaEnem_IgualdadePorConteudo()
    {
        ParametrosDesempateMaiorNotaAreaEnem a = new(["REDACAO", "MATEMATICA"]);
        ParametrosDesempateMaiorNotaAreaEnem b = new(new List<string> { "REDACAO", "MATEMATICA" });
        ParametrosDesempateMaiorNotaAreaEnem invertida = new(["MATEMATICA", "REDACAO"]);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(invertida, "a ordem é a prioridade do desempate");
    }

    [Fact(DisplayName = "Parametros de área do ENEM sob outro código de regra são incompatíveis")]
    public void Criar_ParametrosDeAreaSobOutraRegra_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorIdade), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO"]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.ParametrosIncompativeisComRegra");
    }

    [Fact(DisplayName = "Ordem de áreas vazia é recusada")]
    public void Criar_MaiorNotaAreaEnem_SemAreas_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new ParametrosDesempateMaiorNotaAreaEnem([]));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("areas");
        erro.Error.Code.Should().Be("CriterioDesempate.AreasObrigatorias");
    }

    [Fact(DisplayName = "Área repetida na ordem de desempate é recusada no campo do item repetido")]
    public void Criar_MaiorNotaAreaEnem_AreaRepetida_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO", "MATEMATICA", "REDACAO"]));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("areas[2]");
        erro.Error.Code.Should().Be("CriterioDesempate.AreaRepetida");
        erro.Error.Message.Should().Contain("REDACAO");
    }

    [Theory(DisplayName = "Código de área fora da forma do cadastro é recusado no campo do item")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("redacao")]
    [InlineData("REDAÇÃO")]
    [InlineData("REDACAO\n")]
    [InlineData("UM_CODIGO_BEM_MAIOR_QUE_TRINTA_CARACTERES")]
    public void Criar_MaiorNotaAreaEnem_CodigoInvalido_Falha(string codigo)
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new ParametrosDesempateMaiorNotaAreaEnem(["MATEMATICA", codigo]));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("areas[1]");
        erro.Error.Code.Should().Be("CriterioDesempate.AreaInvalida");
    }

    [Fact(DisplayName = "Ordem de áreas acima do teto é recusada")]
    public void Criar_MaiorNotaAreaEnem_AreasEmExcesso_Falha()
    {
        string[] areas = [.. Enumerable.Range(0, ParametrosDesempateMaiorNotaAreaEnem.AreasMaximo + 1).Select(static i => $"AREA_{i}")];

        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new ParametrosDesempateMaiorNotaAreaEnem(areas));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreasEmExcesso");
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida e área repetida acumulam no mesmo lote")]
    public void Criar_OrdemInvalidaEAreaRepetida_AcumulaAsDuasViolacoes()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            0, Regra(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO", "REDACAO"]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "CriterioDesempate.OrdemInvalida",
            "CriterioDesempate.AreaRepetida",
        ]);
    }

    [Fact(DisplayName = "ValidarOrdem sem violação não devolve recusa")]
    public void ValidarOrdem_SemViolacao_Vazio()
    {
        List<FieldError> erros = CriterioDesempate.ValidarOrdem(1);

        erros.Should().BeEmpty();
    }
}
