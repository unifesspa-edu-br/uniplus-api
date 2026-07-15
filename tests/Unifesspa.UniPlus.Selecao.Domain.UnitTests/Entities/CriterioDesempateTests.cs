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
}
