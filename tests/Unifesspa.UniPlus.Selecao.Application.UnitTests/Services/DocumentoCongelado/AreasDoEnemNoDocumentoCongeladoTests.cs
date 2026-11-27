namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoCongelado;

using System.Globalization;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoCongelado;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class AreasDoEnemNoDocumentoCongeladoTests
{
    private const string BaseLegal = "Resolução nº 805/2024/Consepe – Anexo I";

    [Fact(DisplayName = "Os grupos saem na ordem ordinal do código, sem depender da ordem em que chegam nem da cultura")]
    public void QuadroDePesos_GruposForaDeOrdem_SaemEmOrdemOrdinalDoCodigo()
    {
        JsonArray quadro = AreasDoEnemNoDocumentoCongelado.QuadroDePesos(
            [GrupoComArea("GRUPO_2"), GrupoComArea("GRUPO_10"), GrupoComArea("A_GRUPO"), GrupoComArea("B_GRUPO")]);

        quadro.Select(static grupo => grupo!["grupoAreaEnem"]!["codigo"]!.GetValue<string>())
            .Should().Equal("A_GRUPO", "B_GRUPO", "GRUPO_10", "GRUPO_2");
    }

    [Fact(DisplayName = "As áreas de cada grupo saem na ordem ordinal do código")]
    public void QuadroDePesos_AreasForaDeOrdem_SaemEmOrdemOrdinalDoCodigo()
    {
        GrupoPesoAreaEnemCongelado grupo = Grupo("TECNOLOGICA", [("REDACAO", "Redação", 2m, null), ("AREA_10", "Dez", 1m, null), ("AREA_2", "Dois", 1m, null)]);

        JsonArray areas = AreasDoEnemNoDocumentoCongelado.QuadroDePesos([grupo])[0]!["areas"]!.AsArray();

        areas.Select(static area => area!["codigo"]!.GetValue<string>()).Should().Equal("AREA_10", "AREA_2", "REDACAO");
    }

    [Fact(DisplayName = "Código, rótulo e base legal do grupo e da área saem em NFC")]
    public void QuadroDePesos_TextosDecompostos_SaemEmNfc()
    {
        GrupoPesoAreaEnemCongelado grupo = Grupo("SAUDE", "Saúde", "Base legál", [("CIENCIAS", "Ciências", 1m, null)]);

        JsonObject escrito = (JsonObject)AreasDoEnemNoDocumentoCongelado.QuadroDePesos([grupo])[0]!;

        escrito["grupoAreaEnem"]!["rotulo"]!.GetValue<string>().Should().Be("Saúde");
        escrito["baseLegal"]!.GetValue<string>().Should().Be("Base legál");
        escrito["areas"]![0]!["rotulo"]!.GetValue<string>().Should().Be("Ciências");
    }

    [Fact(DisplayName = "O grupo de áreas do Enem sai com código e rótulo em NFC")]
    public void Grupo_TextosDecompostos_SaemEmNfc()
    {
        GrupoAreaEnemSnapshot grupo = GrupoAreaEnemSnapshot.Criar("SAUDE", "Saúde").Value!;

        JsonObject escrito = AreasDoEnemNoDocumentoCongelado.Grupo(grupo);

        escrito["codigo"]!.GetValue<string>().Should().Be("SAUDE");
        escrito["rotulo"]!.GetValue<string>().Should().Be("Saúde");
    }

    [Theory(DisplayName = "Peso e corte saem como texto com quatro casas, arredondados ao par mais próximo")]
    [InlineData("3", "3.0000")]
    [InlineData("1.5", "1.5000")]
    [InlineData("400.125", "400.1250")]
    [InlineData("1.23455", "1.2346")]
    [InlineData("1.23445", "1.2344")]
    public void QuadroDePesos_NumeroComMaisOuMenosDeQuatroCasas_SaiComQuatroCasas(string valor, string esperado)
    {
        decimal numero = decimal.Parse(valor, CultureInfo.InvariantCulture);
        GrupoPesoAreaEnemCongelado grupo = Grupo("TECNOLOGICA", [("REDACAO", "Redação", numero, numero)]);

        JsonNode? area = AreasDoEnemNoDocumentoCongelado.QuadroDePesos([grupo])[0]!["areas"]![0];

        area!["peso"]!.GetValue<string>().Should().Be(esperado);
        area["corte"]!.GetValue<string>().Should().Be(esperado);
    }

    [Fact(DisplayName = "A área sem corte sai com a chave corte nula e explícita, junto das outras três chaves")]
    public void QuadroDePesos_AreaSemCorte_EscreveCorteNulo()
    {
        GrupoPesoAreaEnemCongelado grupo = Grupo("TECNOLOGICA", [("MATEMATICA", "Matemática", 1.5m, null)]);

        JsonObject area = AreasDoEnemNoDocumentoCongelado.QuadroDePesos([grupo])[0]!["areas"]![0]!.AsObject();

        area.Select(static propriedade => propriedade.Key).Should().Equal("codigo", "rotulo", "peso", "corte");
        area["corte"].Should().BeNull();
    }

    [Fact(DisplayName = "O grupo do quadro traz as chaves grupoAreaEnem, baseLegal e areas")]
    public void QuadroDePesos_Grupo_TrazAsTresChaves()
    {
        JsonObject escrito = (JsonObject)AreasDoEnemNoDocumentoCongelado.QuadroDePesos([GrupoComArea("TECNOLOGICA")])[0]!;

        escrito.Select(static propriedade => propriedade.Key).Should().Equal("grupoAreaEnem", "baseLegal", "areas");
    }

    private static GrupoPesoAreaEnemCongelado GrupoComArea(string codigo) =>
        Grupo(codigo, [("LINGUAGENS", "Linguagens", 1m, null)]);

    private static GrupoPesoAreaEnemCongelado Grupo(string codigo, IEnumerable<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> areas) =>
        Grupo(codigo, "Rótulo do grupo", areas);

    private static GrupoPesoAreaEnemCongelado Grupo(
        string codigo, string rotulo, IEnumerable<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> areas) =>
        Grupo(codigo, rotulo, BaseLegal, areas);

    private static GrupoPesoAreaEnemCongelado Grupo(
        string codigo, string rotulo, string baseLegal, IEnumerable<(string Codigo, string Rotulo, decimal Peso, decimal? Corte)> areas) =>
        GrupoPesoAreaEnemCongelado.Criar(
            codigo, rotulo, baseLegal, areas.Select(static area => ((string?)area.Codigo, (string?)area.Rotulo, area.Peso, area.Corte))).Value!;
}
