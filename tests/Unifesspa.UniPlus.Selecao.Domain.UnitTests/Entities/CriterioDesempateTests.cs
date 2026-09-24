namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CriterioDesempateTests
{
    private static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.Criar(codigo, "v1", new string('a', 64)).Value!;

    private static CondicaoDnf CondicaoProfessorRural() =>
        CondicaoDnf.Criar("PROFESSOR_RURAL", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

    [Fact(DisplayName = "Criar DESEMPATE-MAIOR-NOTA-ETAPA com args compatíveis tem sucesso")]
    public void Criar_MaiorNotaEtapa_Sucesso()
    {
        Guid etapaId = Guid.CreateVersion7();
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaEtapa), new ArgsDesempateMaiorNotaEtapa(etapaId));

        resultado.IsSuccess.Should().BeTrue();
        ((ArgsDesempateMaiorNotaEtapa)resultado.Value!.Args).EtapaRef.Should().Be(etapaId);
    }

    [Fact(DisplayName = "Criar DESEMPATE-MAIOR-IDADE (sem args) tem sucesso")]
    public void Criar_MaiorIdade_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorIdade), new ArgsDesempateMaiorIdade());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar DESEMPATE-IDOSO com idade mínima válida tem sucesso")]
    public void Criar_Idoso_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.Idoso), new ArgsDesempateIdoso(60));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar DESEMPATE-PREDICADO-FATO com args completos tem sucesso")]
    public void Criar_PredicadoFato_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.PredicadoFato), new ArgsDesempatePredicadoFato(CondicaoProfessorRural()));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar DESEMPATE-PREDICADO-FATO com fato fora do vocabulário fechado falha")]
    public void Criar_PredicadoFato_FatoDesconhecido_Falha()
    {
        Dictionary<string, DescritorFatoCandidato> vocabulario = new()
        {
            ["PROFESSOR_RURAL"] = DescritorFatoCandidato.Criar("PROFESSOR_RURAL", TipoDominioFato.Booleano, null).Value!,
        };

        CondicaoDnf condicaoDesconhecida = CondicaoDnf.Criar("FATO_INEXISTENTE", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.PredicadoFato), new ArgsDesempatePredicadoFato(condicaoDesconhecida), vocabulario);

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
            1, Regra(CriterioDesempateCodigo.PredicadoFato), new ArgsDesempatePredicadoFato(condicao), vocabulario);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be(campo);
    }

    [Fact(DisplayName = "Criar DESEMPATE-PREDICADO-FATO com fato do vocabulário fechado tem sucesso")]
    public void Criar_PredicadoFato_FatoConhecido_Sucesso()
    {
        Dictionary<string, DescritorFatoCandidato> vocabulario = new()
        {
            ["PROFESSOR_RURAL"] = DescritorFatoCandidato.Criar("PROFESSOR_RURAL", TipoDominioFato.Booleano, null).Value!,
        };

        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.PredicadoFato), new ArgsDesempatePredicadoFato(CondicaoProfessorRural()), vocabulario);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com ordem não positiva falha")]
    public void Criar_OrdemInvalida_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            0, Regra(CriterioDesempateCodigo.MaiorIdade), new ArgsDesempateMaiorIdade());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.OrdemInvalida");
    }

    [Fact(DisplayName = "Criar com args incompatíveis com a regra falha")]
    public void Criar_ArgsIncompativeis_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaEtapa), new ArgsDesempateMaiorIdade());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.ArgsIncompativeisComRegra");
    }

    [Fact(DisplayName = "Criar DESEMPATE-IDOSO com idade mínima não positiva falha")]
    public void Criar_IdadeMinimaInvalida_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.Idoso), new ArgsDesempateIdoso(0));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.IdadeMinimaInvalida");
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida e idade mínima inválida acumulam no mesmo lote")]
    public void Criar_OrdemInvalidaEIdadeMinimaInvalida_AcumulaAsDuasViolacoes()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            0, Regra(CriterioDesempateCodigo.Idoso), new ArgsDesempateIdoso(0));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "CriterioDesempate.OrdemInvalida",
            "CriterioDesempate.IdadeMinimaInvalida",
        ]);
    }

    [Fact(DisplayName = "Criar DESEMPATE-MAIOR-NOTA-AREA-ENEM guarda os códigos na ordem declarada")]
    public void Criar_MaiorNotaAreaEnem_Sucesso()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO", "MATEMATICA"]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ((ArgsDesempateMaiorNotaAreaEnem)resultado.Value!.Args).Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Mudar a lista entregue depois da validação não altera a ordem de áreas do critério")]
    public void Criar_MaiorNotaAreaEnem_ListaDeOrigemMutadaDepois_NaoAlteraOCriterio()
    {
        string[] areas = ["REDACAO", "MATEMATICA"];
        CriterioDesempate criterio = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem), new ArgsDesempateMaiorNotaAreaEnem(areas)).Value!;

        areas[0] = "redacao";
        areas[1] = "REDACAO";

        ((ArgsDesempateMaiorNotaAreaEnem)criterio.Args).Areas.Should().Equal("REDACAO", "MATEMATICA");
    }

    [Fact(DisplayName = "Args de área do ENEM são iguais pelo conteúdo e pela ordem, como as variantes escalares")]
    public void ArgsMaiorNotaAreaEnem_IgualdadePorConteudo()
    {
        ArgsDesempateMaiorNotaAreaEnem a = new(["REDACAO", "MATEMATICA"]);
        ArgsDesempateMaiorNotaAreaEnem b = new(new List<string> { "REDACAO", "MATEMATICA" });
        ArgsDesempateMaiorNotaAreaEnem invertida = new(["MATEMATICA", "REDACAO"]);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(invertida, "a ordem é a prioridade do desempate");
    }

    [Fact(DisplayName = "Args de área do ENEM sob outro código de regra são incompatíveis")]
    public void Criar_ArgsDeAreaSobOutraRegra_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorIdade), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO"]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.ArgsIncompativeisComRegra");
    }

    [Fact(DisplayName = "Ordem de áreas vazia é recusada")]
    public void Criar_MaiorNotaAreaEnem_SemAreas_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem), new ArgsDesempateMaiorNotaAreaEnem([]));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("areas");
        erro.Error.Code.Should().Be("CriterioDesempate.AreasObrigatorias");
    }

    [Fact(DisplayName = "Área repetida na ordem de desempate é recusada no campo do item repetido")]
    public void Criar_MaiorNotaAreaEnem_AreaRepetida_Falha()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO", "MATEMATICA", "REDACAO"]));

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
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem), new ArgsDesempateMaiorNotaAreaEnem(["MATEMATICA", codigo]));

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("areas[1]");
        erro.Error.Code.Should().Be("CriterioDesempate.AreaInvalida");
    }

    [Fact(DisplayName = "Ordem de áreas acima do teto é recusada")]
    public void Criar_MaiorNotaAreaEnem_AreasEmExcesso_Falha()
    {
        string[] areas = [.. Enumerable.Range(0, CriterioDesempate.AreasMaximo + 1).Select(static i => $"AREA_{i}")];

        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            1, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem), new ArgsDesempateMaiorNotaAreaEnem(areas));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CriterioDesempate.AreasEmExcesso");
    }

    [Fact(DisplayName = "ADR-0125: ordem inválida e área repetida acumulam no mesmo lote")]
    public void Criar_OrdemInvalidaEAreaRepetida_AcumulaAsDuasViolacoes()
    {
        Result<CriterioDesempate> resultado = CriterioDesempate.Criar(
            0, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO", "REDACAO"]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "CriterioDesempate.OrdemInvalida",
            "CriterioDesempate.AreaRepetida",
        ]);
    }

    [Fact(DisplayName = "ValidarOrdem sem violação retorna lote vazio")]
    public void ValidarOrdem_SemViolacao_Vazio()
    {
        List<FieldError> erros = CriterioDesempate.ValidarOrdem(1);

        erros.Should().BeEmpty();
    }
}
