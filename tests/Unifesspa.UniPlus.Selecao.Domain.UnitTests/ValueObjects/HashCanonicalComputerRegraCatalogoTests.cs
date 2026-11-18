namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class HashCanonicalComputerRegraCatalogoTests
{
    [Fact]
    public void Hash_MesmaDefinicao_EhDeterministico()
    {
        RegraCatalogo primeira = Criar();
        RegraCatalogo segunda = Criar();

        primeira.Hash.Should().Be(segunda.Hash);
        primeira.RecomputeHash().Should().Be(primeira.Hash);
    }

    [Fact]
    public void Hash_OrdemDasChaves_NaoAltera()
    {
        RegraCatalogo primeira = Criar(declaracao: """{"a":1,"b":2}""");
        RegraCatalogo segunda = Criar(declaracao: """{"b":2,"a":1}""");

        primeira.Hash.Should().Be(segunda.Hash);
    }

    [Theory]
    [InlineData("codigo")]
    [InlineData("versao")]
    [InlineData("tipo")]
    [InlineData("declaracao")]
    [InlineData("invariantes")]
    [InlineData("baseLegal")]
    public void Hash_MudancaEmCadaCampo_AlteraHash(string campo)
    {
        RegraCatalogo original = Criar();
        RegraCatalogo alterada = Criar(
            codigo: campo == "codigo" ? "OUTRA" : "REGRA",
            versao: campo == "versao" ? "v2" : "v1",
            tipo: campo == "tipo" ? TipoRegra.RegraArredondamento : TipoRegra.RegraBonus,
            declaracao: campo == "declaracao" ? """{"a":2}""" : """{"a":1}""",
            invariantes: campo == "invariantes" ? """["outra"]""" : """["condição"]""",
            baseLegal: campo == "baseLegal" ? "Outra base" : "Base legal");

        alterada.Hash.Should().NotBe(original.Hash);
    }

    private static RegraCatalogo Criar(
        string codigo = "REGRA",
        string versao = "v1",
        TipoRegra tipo = TipoRegra.RegraBonus,
        string declaracao = """{"a":1}""",
        string invariantes = """["condição"]""",
        string baseLegal = "Base legal") =>
        RegraCatalogo.Criar(codigo, versao, tipo, Json(declaracao), Json(invariantes), baseLegal);

    private static JsonElement Json(string texto)
    {
        using JsonDocument documento = JsonDocument.Parse(texto);
        return documento.RootElement.Clone();
    }
}
