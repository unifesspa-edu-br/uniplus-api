namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

public sealed class DeclaracoesVaziasDoCatalogoTests
{
    [Theory]
    [InlineData("PRECISAO-TRUNCAR")]
    [InlineData("PRECISAO-ARREDONDAR-CIMA")]
    [InlineData("BONUS-MULTIPLICATIVO")]
    [InlineData("RECURSO-PRAZO-ANCORADO-EM-ATO")]
    [InlineData("RENDA-PER-CAPITA-LEI-12711")]
    [InlineData("RECONCILIACAO-VAGAS-ARTIGO-11-PARAGRAFO-UNICO")]
    public void Declaracao_DaRegraSemDeclaracaoDeVersao_EhVazia(string codigo)
    {
        RegraCatalogoSeedItem item = RegraCatalogoSeed.Itens.Single(i => i.Codigo == codigo);
        using JsonDocument documento = JsonDocument.Parse(item.DeclaracaoDaRegraJson);
        documento.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        documento.RootElement.EnumerateObject().Should().BeEmpty();
    }
}
