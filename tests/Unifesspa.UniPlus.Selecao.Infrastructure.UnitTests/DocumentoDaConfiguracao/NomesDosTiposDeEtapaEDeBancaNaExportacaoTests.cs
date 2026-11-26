namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class NomesDosTiposDeEtapaEDeBancaNaExportacaoTests
{
    private readonly JsonObject _congelado = JsonNode.Parse(EnvelopesDeReferenciaDeTeste.Ler("envelope-rico.json"))!.AsObject();

    [Fact]
    public void Exportar_EtapasDoCongelado_LevamONomeDoTipoDeEtapa()
    {
        JsonObject documento = Exportar();

        Textos(documento["etapas"], "tipoEtapaNome").Should().Equal(Textos(_congelado["etapas"], "tipoEtapa", "nome"));
        Textos(documento["etapas"], "tipoEtapaNome").Should().NotBeEmpty();
    }

    [Fact]
    public void Exportar_BancasDaEtapa_LevamONomeDoTipoDeBanca()
    {
        JsonObject documento = Exportar();

        Textos(documento["etapas"], "bancas", "tipoBancaNome").Should().Equal(Textos(_congelado["etapas"], "bancas", "nome"));
        Textos(documento["etapas"], "bancas", "tipoBancaNome").Should().NotBeEmpty();
    }

    [Fact]
    public void Exportar_BancasDaFase_LevamONomeDoTipoDeBanca()
    {
        JsonObject documento = Exportar();

        Textos(documento["cronograma"]!["fases"], "bancasRequeridas", "tipoBancaNome")
            .Should().Equal(Textos(_congelado["cronogramaFases"]!["fases"], "bancasRequeridas", "nome"));
        Textos(documento["cronograma"]!["fases"], "bancasRequeridas", "tipoBancaNome").Should().NotBeEmpty();
    }

    private JsonObject Exportar() => new ProjecaoDaVersaoPublicada(new EsquemaDoDocumentoDaConfiguracao())
        .Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, _congelado.ToJsonString()));

    private static List<string> Textos(JsonNode? lista, params string[] caminho)
    {
        IEnumerable<JsonNode?> nos = (lista as JsonArray) ?? [];
        foreach (string passo in caminho)
        {
            nos = nos.SelectMany(no => Descer(no, passo));
        }

        return [.. nos.Select(static no => no!.GetValue<string>())];
    }

    private static JsonNode?[] Descer(JsonNode? no, string nome) => no?[nome] switch
    {
        JsonArray lista => [.. lista],
        { } valor => [valor],
        _ => [],
    };
}
