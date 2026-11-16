namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O código do tipo de processo é gravado em NFC; a checagem de código já cadastrado compara
/// pela mesma forma, então o código decomposto não escapa dela.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
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

        HttpResponseMessage primeiro = await EnviarPostAdmin(client, $"PÓS_{sufixo}");
        HttpResponseMessage repetido = await EnviarPostAdmin(client, $"PÓS_{sufixo}");

        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);
        repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, string codigo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { codigo, nome = "Seleção de teste", descricao = "Descrição" });
        return await client.SendAsync(request);
    }
}
