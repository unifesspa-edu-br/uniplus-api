namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosDoDadoEndpointTests(ConfiguracaoEndpointFixture fixture)
{
    private sealed record Item(string Codigo, string Rotulo);

    [Fact]
    public async Task ListarTiposDoDado_SemAutenticacao_DevolveCodigosERotulos()
    {
        using HttpClient client = fixture.Factory.CreateDefaultClient();
        Item[] itens = await ApoioDosVocabularios.LerAsync<Item>(client, "tipos-do-dado", "tipo-do-dado");
        itens.Should().Equal(
            new Item("SIM_OU_NAO", "Sim ou não"), new Item("OPCAO", "Opção"), new Item("NUMERICO", "Número"),
            new Item("TEXTO", "Texto"), new Item("DATA", "Data"), new Item("ENDERECO", "Endereço"));
    }

    [Fact]
    public async Task ListarQuantidadesDeValores_SemAutenticacao_DevolveCodigosERotulos()
    {
        using HttpClient client = fixture.Factory.CreateDefaultClient();
        Item[] itens = await ApoioDosVocabularios.LerAsync<Item>(client, "quantidades-de-valores", "quantidade-de-valores");
        itens.Should().Equal(new Item("VALOR_UNICO", "Um valor"), new Item("VARIOS_VALORES", "Vários valores"));
    }
}
