namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Cobre CA-04 e CA-05 da Story #847 (ADR-0111).</summary>
public sealed class CondicaoDnfTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    [Fact(DisplayName = "CondicaoDnf_Rejeita_Fato_Vazio")]
    public void CondicaoDnf_Rejeita_Fato_Vazio()
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.Criar("   ", Operador.Igual, Json("true"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.FatoObrigatorio");
    }

    [Fact(DisplayName = "CondicaoDnf_Rejeita_Operador_Sentinela")]
    public void CondicaoDnf_Rejeita_Operador_Sentinela()
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.Criar("SEXO", Operador.Nenhuma, Json("\"FEMININO\""));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.OperadorInvalido");
    }

    [Theory(DisplayName = "CondicaoDnf aceita as quatro variantes válidas de operador com valor coerente")]
    [InlineData(Operador.Igual, "true")]
    [InlineData(Operador.Em, "[\"BRANCA\"]")]
    [InlineData(Operador.MaiorIgual, "18")]
    [InlineData(Operador.MenorIgual, "60")]
    public void CondicaoDnf_Aceita_Operadores_Validos(Operador operador, string valorJson)
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.Criar("FATO", operador, Json(valorJson));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "CondicaoDnf_Em_Rejeita_Valor_Nao_Lista")]
    public void CondicaoDnf_Em_Rejeita_Valor_Nao_Lista()
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.Criar("COR_RACA", Operador.Em, Json("\"BRANCA\""));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.FormaIncoerenteComOperador");
    }

    [Fact(DisplayName = "CondicaoDnf_Em_Rejeita_Lista_Vazia")]
    public void CondicaoDnf_Em_Rejeita_Lista_Vazia()
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.Criar("COR_RACA", Operador.Em, Json("[]"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.FormaIncoerenteComOperador");
    }

    [Theory(DisplayName = "CondicaoDnf_Escalar_Rejeita_Lista")]
    [InlineData(Operador.Igual)]
    [InlineData(Operador.MaiorIgual)]
    [InlineData(Operador.MenorIgual)]
    public void CondicaoDnf_Escalar_Rejeita_Lista(Operador operador)
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.Criar("FATO", operador, Json("[1,2]"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.FormaIncoerenteComOperador");
    }

    [Fact(DisplayName = "CondicaoDnf_Escalar_Rejeita_Objeto")]
    public void CondicaoDnf_Escalar_Rejeita_Objeto()
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.Criar("FATO", Operador.Igual, Json("{}"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.FormaIncoerenteComOperador");
    }
}
