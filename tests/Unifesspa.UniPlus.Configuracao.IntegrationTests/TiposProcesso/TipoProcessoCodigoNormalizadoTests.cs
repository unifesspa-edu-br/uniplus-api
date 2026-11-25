namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.RequisicoesDeTipoProcesso;

/// <summary>O código é conferido como recebido, sem normalização.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoProcessoCodigoNormalizadoTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoProcessoCodigoNormalizadoTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST recusa códigos com acento composto ou decomposto")]
    public async Task Criar_CodigoComAcento_RecusaFormato()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage primeiro = await EnviarTipoComCodigo(client, $"PÓS_{sufixo}");
        HttpResponseMessage repetido = await EnviarTipoComCodigo(client, $"PÓS_{sufixo}");

        primeiro.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        repetido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static Task<HttpResponseMessage> EnviarTipoComCodigo(HttpClient client, string codigo) =>
        EnviarPostAdmin(client, new { codigo, nome = "Seleção de teste", descricao = "Descrição" });
}
