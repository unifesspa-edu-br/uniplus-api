namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ProvaPresencialNoEnvelopeTests
{
    private static DeclaracaoDaProvaPresencial Declaracao() => DeclaracaoDaProvaPresencial.Declarar(true,
        [new("1504208", "PA"), new("1501402", "PA"), new("3550308", "SP")], 100).Value!
        .ComNomesDoGeo(MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            ["1504208"] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
            ["1501402"] = ResultadoDoMunicipioNoGeo.Conferido("Belém"),
            ["3550308"] = ResultadoDoMunicipioNoGeo.Conferido("São Paulo"),
        }));

    [Fact]
    public void Serializar_ComMunicipios_PreservaOrdemENomes()
    {
        JsonObject bloco = SerializacaoDaProvaPresencial.Serializar(Declaracao());
        JsonArray municipios = bloco["municipios"]!.AsArray();

        bloco["temProvaPresencial"]!.GetValue<bool>().Should().BeTrue();
        municipios.Select(static m => m!["codigoIbge"]!.GetValue<string>()).Should().Equal("1504208", "1501402", "3550308");
        municipios.Select(static m => m!["uf"]!.GetValue<string>()).Should().Equal("PA", "PA", "SP");
        municipios.Select(static m => m!["nome"]!.GetValue<string>()).Should().Equal("Marabá", "Belém", "São Paulo");
    }

    [Fact]
    public void Serializar_SemDeclaracaoOuSemProva_EscreveBlocoVazio()
    {
        DeclaracaoDaProvaPresencial semProva = DeclaracaoDaProvaPresencial.Declarar(false, [], 100).Value!;
        JsonObject naoDeclarada = SerializacaoDaProvaPresencial.Serializar(null);
        JsonObject declarada = SerializacaoDaProvaPresencial.Serializar(semProva);

        JsonNode.DeepEquals(naoDeclarada, declarada).Should().BeTrue();
        declarada["temProvaPresencial"]!.GetValue<bool>().Should().BeFalse();
        declarada["municipios"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public void Ler_BlocoSerializado_ReconstroiDeclaracao()
    {
        DeclaracaoDaProvaPresencial original = Declaracao();
        JsonObject envelope = new() { ["provaPresencial"] = SerializacaoDaProvaPresencial.Serializar(original) };
        LeitorEnvelope leitor = new();

        DeclaracaoDaProvaPresencial? lida = LeituraDaProvaPresencial.Ler(leitor, envelope);

        leitor.Falhou.Should().BeFalse();
        lida.Should().Be(original);
    }

    [Fact]
    public void Ler_BlocoAusente_Recusa()
    {
        LeitorEnvelope leitor = new();

        LeituraDaProvaPresencial.Ler(leitor, []);

        leitor.Falhou.Should().BeTrue();
    }

    [Theory]
    [InlineData("temProvaPresencial")]
    [InlineData("municipios")]
    public void Ler_ChaveDoBlocoAusente_Recusa(string chave)
    {
        JsonObject bloco = SerializacaoDaProvaPresencial.Serializar(Declaracao());
        bloco.Remove(chave);
        LeitorEnvelope leitor = new();

        LeituraDaProvaPresencial.Ler(leitor, new JsonObject { ["provaPresencial"] = bloco });

        leitor.Falhou.Should().BeTrue();
    }

    [Theory]
    [InlineData("codigoIbge")]
    [InlineData("uf")]
    [InlineData("nome")]
    public void Ler_ChaveDoMunicipioAusente_Recusa(string chave)
    {
        JsonObject bloco = SerializacaoDaProvaPresencial.Serializar(Declaracao());
        bloco["municipios"]![0]!.AsObject().Remove(chave);
        LeitorEnvelope leitor = new();

        LeituraDaProvaPresencial.Ler(leitor, new JsonObject { ["provaPresencial"] = bloco });

        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void Hash_MunicipioAlterado_MudaResumo()
    {
        JsonObject envelope = new() { ["provaPresencial"] = SerializacaoDaProvaPresencial.Serializar(Declaracao()) };
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        string original = perfil.HashHex(perfil.Serializar(envelope));

        envelope["provaPresencial"]!["municipios"]![0]!["nome"] = "Outro nome";

        perfil.HashHex(perfil.Serializar(envelope)).Should().NotBe(original);
    }
}
