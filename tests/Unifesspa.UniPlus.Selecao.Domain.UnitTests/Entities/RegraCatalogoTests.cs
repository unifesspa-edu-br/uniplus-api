namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura da factory de <see cref="RegraCatalogo"/>: validação da definição
/// e hash content-addressable computado na criação.
/// </summary>
public sealed class RegraCatalogoTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static Result<RegraCatalogo> CriarValida() => RegraCatalogo.Criar(
        "BONUS-MULTIPLICATIVO", "v1", TipoRegra.RegraBonus,
        Json("""{"fator":"numeric","teto":"numeric|null"}"""),
        Json("""["nota_final × fator, após os pesos"]"""),
        "RN05 + decisão PO");

    [Fact(DisplayName = "Criar com definição válida computa hash content-addressable")]
    public void Criar_Valida_ComputaHash()
    {
        Result<RegraCatalogo> resultado = CriarValida();

        resultado.IsSuccess.Should().BeTrue();
        RegraCatalogo regra = resultado.Value!;
        HashCanonicalComputer.IsValidHashShape(regra.Hash).Should().BeTrue();
        regra.RecomputeHash().Should().Be(regra.Hash, "recomputar a partir do estado deve bater com o hash gravado");
        regra.Tipo.Should().Be(TipoRegra.RegraBonus);
    }

    [Theory(DisplayName = "Criar recusa código/versão/base legal vazios")]
    [InlineData("", "v1", "base")]
    [InlineData("  ", "v1", "base")]
    [InlineData("R", "", "base")]
    [InlineData("R", "v1", "")]
    public void Criar_CamposObrigatoriosVazios_Falha(string codigo, string versao, string baseLegal)
    {
        Result<RegraCatalogo> resultado = RegraCatalogo.Criar(
            codigo, versao, TipoRegra.RegraBonus, Json("{}"), Json("[]"), baseLegal);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar recusa tipo Nenhuma (sentinela)")]
    public void Criar_TipoNenhuma_Falha()
    {
        Result<RegraCatalogo> resultado = RegraCatalogo.Criar(
            "R", "v1", TipoRegra.Nenhuma, Json("{}"), Json("[]"), "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraCatalogo.TipoObrigatorio");
    }

    [Fact(DisplayName = "Criar recusa esquema_args que não é objeto JSON")]
    public void Criar_EsquemaArgsNaoObjeto_Falha()
    {
        Result<RegraCatalogo> resultado = RegraCatalogo.Criar(
            "R", "v1", TipoRegra.RegraBonus, Json("[]"), Json("[]"), "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraCatalogo.EsquemaArgsInvalido");
    }

    [Fact(DisplayName = "Criar recusa invariantes que não é array JSON")]
    public void Criar_InvariantesNaoArray_Falha()
    {
        Result<RegraCatalogo> resultado = RegraCatalogo.Criar(
            "R", "v1", TipoRegra.RegraBonus, Json("{}"), Json("{}"), "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraCatalogo.InvariantesInvalidas");
    }
}
