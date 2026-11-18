namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

public sealed class FormaDosParametrosNoDocumentoTests
{
    [Theory]
    [InlineData("eliminacao.schema.json", TipoRegra.RegraEliminacao)]
    [InlineData("desempate.schema.json", TipoRegra.CriterioDesempate)]
    public void Codigos_DoEsquemaECatalogo_SaoIguais(string arquivo, TipoRegra tipo)
    {
        JsonArray condicoes = ArquivosDoEsquema.DoAssembly().PorNome[arquivo]["allOf"]!.AsArray();
        string[] codigos = condicoes.Select(c => c!["if"]!["properties"]!["regra"]!["properties"]!["codigo"]!["const"]!.GetValue<string>()).ToArray();
        codigos.Should().NotBeEmpty();
        codigos.Should().BeEquivalentTo(RegraCatalogoSeed.Itens.Where(i => i.Tipo == tipo).Select(i => i.Codigo));
    }

    [Theory]
    [InlineData(TipoRegra.RegraEliminacao)]
    [InlineData(TipoRegra.CriterioDesempate)]
    public void Declaracoes_DasRegras_SaoVazias(TipoRegra tipo)
    {
        foreach (RegraCatalogoSeedItem item in RegraCatalogoSeed.Itens.Where(i => i.Tipo == tipo))
        {
            using JsonDocument documento = JsonDocument.Parse(item.DeclaracaoDaRegraJson);
            documento.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
            documento.RootElement.EnumerateObject().Should().BeEmpty(item.Codigo);
        }
    }
}
