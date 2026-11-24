namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

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

    private static RegraCatalogo CriarValida() => RegraCatalogo.Criar(
        "FADA_MULTIPLICATIVO", "v1", TipoRegra.RegraBonus,
        Json("""{"fator":"numeric","teto":"numeric|null"}"""),
        Json("""["nota_final × fator, após os pesos"]"""),
        "Bônus multiplicativo aplicado à nota final após os pesos das áreas do ENEM");

    [Fact(DisplayName = "Criar com definição válida computa hash content-addressable")]
    public void Criar_Valida_ComputaHash()
    {
        RegraCatalogo regra = CriarValida();

        HashCanonicalComputer.IsValidHashShape(regra.Hash).Should().BeTrue();
        regra.RecomputeHash().Should().Be(regra.Hash, "recomputar a partir do estado deve bater com o hash gravado");
        regra.Tipo.Should().Be(TipoRegra.RegraBonus);
    }

    [Fact(DisplayName = "Referencia devolve a identidade content-addressable da regra")]
    public void Referencia_DaRegraCriada_ReproduzCodigoVersaoEHash()
    {
        RegraCatalogo regra = CriarValida();

        ReferenciaRegra referencia = regra.Referencia();

        referencia.Should().Be(ReferenciaRegra.De(regra.Codigo, regra.Versao, regra.Hash));
    }

    [Theory(DisplayName = "Criar recusa código/versão/base legal vazios")]
    [InlineData("", "v1", "base", "codigo")]
    [InlineData("  ", "v1", "base", "codigo")]
    [InlineData("R", "", "base", "versao")]
    [InlineData("R", "v1", "", "baseLegal")]
    public void Criar_CamposObrigatoriosVazios_LancaArgumentException(string codigo, string versao, string baseLegal, string parametro)
    {
        Action criar = () => RegraCatalogo.Criar(codigo, versao, TipoRegra.RegraBonus, Json("{}"), Json("[]"), baseLegal);

        criar.Should().Throw<ArgumentException>().WithParameterName(parametro);
    }

    [Fact(DisplayName = "Criar recusa tipo fora do catálogo")]
    public void Criar_TipoForaDoCatalogo_LancaArgumentOutOfRange()
    {
        Action criar = () => RegraCatalogo.Criar("R", "v1", (TipoRegra)0, Json("{}"), Json("[]"), "base");

        criar.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("valor");
    }

    [Fact(DisplayName = "Criar recusa declaracao_da_regra que não é objeto JSON")]
    public void Criar_DeclaracaoDaRegraNaoObjeto_LancaArgumentException()
    {
        Action criar = () => RegraCatalogo.Criar("R", "v1", TipoRegra.RegraBonus, Json("[]"), Json("[]"), "base");

        criar.Should().Throw<ArgumentException>().WithParameterName("declaracaoDaRegra");
    }

    [Fact(DisplayName = "Criar recusa invariantes que não é array JSON")]
    public void Criar_InvariantesNaoArray_LancaArgumentException()
    {
        Action criar = () => RegraCatalogo.Criar("R", "v1", TipoRegra.RegraBonus, Json("{}"), Json("{}"), "base");

        criar.Should().Throw<ArgumentException>().WithParameterName("invariantes");
    }
}
