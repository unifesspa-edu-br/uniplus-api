namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Cobre CA-02 da Story #847 (ADR-0111).</summary>
public sealed class ClausulaDnfTests
{
    private static CondicaoDnf Condicao(string fato) =>
        CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

    [Fact(DisplayName = "ClausulaDnf_Rejeita_Lista_Vazia")]
    public void ClausulaDnf_Rejeita_Lista_Vazia()
    {
        Result<ClausulaDnf> resultado = ClausulaDnf.Criar([]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ClausulaDnf.ClausulaVazia");
    }

    [Fact(DisplayName = "ClausulaDnf_Rejeita_Lista_Nula")]
    public void ClausulaDnf_Rejeita_Lista_Nula()
    {
        Result<ClausulaDnf> resultado = ClausulaDnf.Criar(null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ClausulaDnf.ClausulaVazia");
    }

    [Fact(DisplayName = "ClausulaDnf aceita uma ou mais condições")]
    public void ClausulaDnf_Aceita_Condicoes()
    {
        Result<ClausulaDnf> resultado = ClausulaDnf.Criar([Condicao("PCD"), Condicao("QUILOMBOLA")]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Condicoes.Should().HaveCount(2);
    }

    // ── Story #916 — avaliação ternária (E lógico) ──

    [Fact(DisplayName = "Avaliar: todas verdadeiras resolve Verdadeiro")]
    public void Avaliar_TodasVerdadeiras_Verdadeiro()
    {
        ClausulaDnf clausula = ClausulaDnf.Criar([Condicao("PCD"), Condicao("QUILOMBOLA")]).Value!;
        Dictionary<string, JsonElement> fatos = new()
        {
            ["PCD"] = JsonSerializer.SerializeToElement(true),
            ["QUILOMBOLA"] = JsonSerializer.SerializeToElement(true),
        };

        clausula.Avaliar(fatos).Should().Be(Ternario.Verdadeiro);
    }

    [Fact(DisplayName = "Avaliar: uma condição Falso faz a cláusula inteira ser Falso, mesmo com outra Indeterminada")]
    public void Avaliar_UmaFalsoVenceSobreIndeterminado()
    {
        ClausulaDnf clausula = ClausulaDnf.Criar([Condicao("PCD"), Condicao("FATO_AUSENTE")]).Value!;
        Dictionary<string, JsonElement> fatos = new() { ["PCD"] = JsonSerializer.SerializeToElement(false) };

        clausula.Avaliar(fatos).Should().Be(Ternario.Falso);
    }

    [Fact(DisplayName = "Avaliar: sem nenhuma Falso, uma Indeterminada faz a cláusula ser Indeterminada")]
    public void Avaliar_SemFalso_IndeterminadoVence()
    {
        ClausulaDnf clausula = ClausulaDnf.Criar([Condicao("PCD"), Condicao("FATO_AUSENTE")]).Value!;
        Dictionary<string, JsonElement> fatos = new() { ["PCD"] = JsonSerializer.SerializeToElement(true) };

        clausula.Avaliar(fatos).Should().Be(Ternario.Indeterminado);
    }
}
