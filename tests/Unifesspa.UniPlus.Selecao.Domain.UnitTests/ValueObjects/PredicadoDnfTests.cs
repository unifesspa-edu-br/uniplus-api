namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Cobre CA-01, CA-03 e CA-11 da Story #847 (ADR-0111).</summary>
public sealed class PredicadoDnfTests
{
    private static CondicaoDnf Booleana(string fato, bool valor) =>
        CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;

    [Fact(DisplayName = "PredicadoDnf_SemClausulas_Avalia_Falso")]
    public void PredicadoDnf_SemClausulas_Avalia_Falso()
    {
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas([]);

        resultado.IsSuccess.Should().BeTrue("zero cláusulas é um estado estruturalmente válido");
        resultado.Value!.Clausulas.Should().BeEmpty();
        resultado.Value.Avaliar(new Dictionary<string, JsonElement>()).Should().BeFalse();
    }

    [Fact(DisplayName = "PredicadoDnf_CriarDeCondicoesAgrupadas_Ignora_Ordinais_Ausentes")]
    public void PredicadoDnf_CriarDeCondicoesAgrupadas_Ignora_Ordinais_Ausentes()
    {
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (1, Booleana("PCD", true)),
            (3, Booleana("QUILOMBOLA", true)),
            (5, Booleana("EGRESSO_ESCOLA_PUBLICA", true)),
        ]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Clausulas.Should().HaveCount(3, "os ordinais 2 e 4 ausentes não geram cláusulas vazias");
    }

    [Fact(DisplayName = "PredicadoDnf_Avaliar_Combina_Clausulas_Ou_E_Condicoes_E")]
    public void PredicadoDnf_Avaliar_Combina_Clausulas_Ou_E_Condicoes_E()
    {
        // (PCD=true E QUILOMBOLA=true) OU (EGRESSO_ESCOLA_PUBLICA=true)
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (1, Booleana("PCD", true)),
            (1, Booleana("QUILOMBOLA", true)),
            (2, Booleana("EGRESSO_ESCOLA_PUBLICA", true)),
        ]);
        PredicadoDnf predicado = resultado.Value!;

        Dictionary<string, JsonElement> soPcd = new()
        {
            ["PCD"] = JsonSerializer.SerializeToElement(true),
            ["QUILOMBOLA"] = JsonSerializer.SerializeToElement(false),
            ["EGRESSO_ESCOLA_PUBLICA"] = JsonSerializer.SerializeToElement(false),
        };
        predicado.Avaliar(soPcd).Should().BeFalse("a primeira cláusula exige AMBAS as condições");

        Dictionary<string, JsonElement> soEgresso = new()
        {
            ["PCD"] = JsonSerializer.SerializeToElement(false),
            ["QUILOMBOLA"] = JsonSerializer.SerializeToElement(false),
            ["EGRESSO_ESCOLA_PUBLICA"] = JsonSerializer.SerializeToElement(true),
        };
        predicado.Avaliar(soEgresso).Should().BeTrue("a segunda cláusula sozinha já satisfaz o OU");
    }

    [Fact(DisplayName = "PredicadoDnf_Avaliar_Fato_Nao_Resolvivel_E_Falso_Conservador")]
    public void PredicadoDnf_Avaliar_Fato_Nao_Resolvivel_E_Falso_Conservador()
    {
        Result<PredicadoDnf> resultado = PredicadoDnf.CriarDeCondicoesAgrupadas([(1, Booleana("SEXO_DESCONHECIDO", true))]);
        PredicadoDnf predicado = resultado.Value!;

        Action avaliar = () => predicado.Avaliar(new Dictionary<string, JsonElement>());

        avaliar.Should().NotThrow();
        predicado.Avaliar(new Dictionary<string, JsonElement>()).Should().BeFalse();
    }
}
