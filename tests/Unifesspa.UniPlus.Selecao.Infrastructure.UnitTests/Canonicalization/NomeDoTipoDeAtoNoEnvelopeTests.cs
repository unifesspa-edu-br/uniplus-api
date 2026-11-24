namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class NomeDoTipoDeAtoNoEnvelopeTests
{
    [Fact]
    public void SerializarTiposDeAto_NomeDecomposto_GravaEmNfcERestaura()
    {
        TipoDeAtoCongelado tipo = new("RESULTADO", "Selec\u0327a\u0303o", false, true, false);
        JsonArray tipos = SerializacaoDoEdital.SerializarTiposDeAto(new Dictionary<string, TipoDeAtoCongelado> { [tipo.Codigo] = tipo });
        LeitorEnvelope leitor = new();
        Dictionary<string, TipoDeAtoCongelado> restaurados = Ler(leitor, tipos);

        leitor.Falhou.Should().BeFalse();
        tipos[0]!["nome"]!.GetValue<string>().Should().Be("Seleção");
        restaurados["RESULTADO"].Nome.Should().Be("Seleção");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LerTiposDeAto_NomeAusenteOuVazio_Recusa(string? nome)
    {
        JsonObject tipo = Tipo(nome);
        if (nome is null)
        {
            tipo.Remove("nome");
        }
        LeitorEnvelope leitor = new();
        Ler(leitor, new JsonArray(tipo));
        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void LerTiposDeAto_NomeAcimaDoLimite_Recusa()
    {
        LeitorEnvelope leitor = new();
        Ler(leitor, new JsonArray(Tipo(new string('a', LimitesDoEnvelope.TipoAtoNome + 1))));
        leitor.Falhou.Should().BeTrue();
    }

    private static Dictionary<string, TipoDeAtoCongelado> Ler(LeitorEnvelope leitor, JsonArray tipos) =>
        LeituraDosTiposDeAto.LerTiposDeAto(leitor, new JsonObject { ["tiposDeAto"] = tipos }, [], []);

    private static JsonObject Tipo(string? nome) => new()
    {
        ["codigo"] = "RESULTADO",
        ["nome"] = nome,
        ["unicoPorObjeto"] = false,
        ["ehResultado"] = true,
        ["congelaConfiguracao"] = false,
    };
}
