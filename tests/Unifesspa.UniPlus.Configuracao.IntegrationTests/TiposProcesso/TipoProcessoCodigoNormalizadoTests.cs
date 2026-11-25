namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.RequisicoesDeTipoProcesso;

/// <summary>
/// O código do tipo de processo é gravado em NFC; a checagem de código já cadastrado compara
/// pela mesma forma, então o código decomposto não escapa dela.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoProcessoCodigoNormalizadoTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoProcessoCodigoNormalizadoTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST com o código decomposto de um código já cadastrado em NFC devolve 409")]
    public async Task Criar_CodigoDecompostoJaCadastradoEmNfc_Retorna409()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage primeiro = await EnviarTipoComCodigo(client, $"PÓS_{sufixo}");
        HttpResponseMessage repetido = await EnviarTipoComCodigo(client, $"PÓS_{sufixo}");

        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);
        repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static Task<HttpResponseMessage> EnviarTipoComCodigo(HttpClient client, string codigo) =>
        EnviarPostAdmin(client, new { codigo, nome = "Seleção de teste", descricao = "Descrição" });
}
