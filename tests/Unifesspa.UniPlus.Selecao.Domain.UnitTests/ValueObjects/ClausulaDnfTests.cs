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
}
