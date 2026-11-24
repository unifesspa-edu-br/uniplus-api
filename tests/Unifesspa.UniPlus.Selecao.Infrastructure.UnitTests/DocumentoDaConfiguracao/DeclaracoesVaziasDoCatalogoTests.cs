namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

public sealed class DeclaracoesVaziasDoCatalogoTests
{
    [Theory]
    [InlineData("PRECISAO_TRUNCAR")]
    [InlineData("PRECISAO_ARREDONDAR_A_PARTIR_DE_CINCO")]
    [InlineData("FADA_MULTIPLICATIVO")]
    [InlineData("RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO")]
    [InlineData("RENDA-PER-CAPITA-LEI-12711")]
    [InlineData("AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO")]
    public void Declaracao_DaRegraSemDeclaracaoDeVersao_EhVazia(string codigo)
    {
        RegraCatalogoSeedItem item = RegraCatalogoSeed.Itens.Single(i => i.Codigo == codigo);
        using JsonDocument documento = JsonDocument.Parse(item.DeclaracaoDaRegraJson);
        documento.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        documento.RootElement.EnumerateObject().Should().BeEmpty();
    }
}
