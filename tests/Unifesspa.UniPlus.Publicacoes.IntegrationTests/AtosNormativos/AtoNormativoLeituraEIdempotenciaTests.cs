namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Net.Http.Json;
using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static AtoNormativoApoio;

/// <summary>
/// Leitura pública, autorização e idempotência dos endpoints de <c>AtoNormativo</c>
/// contra o monólito real com Postgres efêmero.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class AtoNormativoLeituraEIdempotenciaTests
{
    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtoNormativoApoio _apoio;

    public AtoNormativoLeituraEIdempotenciaTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoApoio(fixture);
    }

    [Fact(DisplayName = "GET atos retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(BaseAtos, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.ato-normativo.v1+json");
    }

    [Fact(DisplayName = "GET atos/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{BaseAtos}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        string corpo = await response.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.publicacoes.ato_normativo.nao_encontrado");
    }

    [Fact(DisplayName = "POST admin sem autenticação retorna 401")]
    public async Task Registrar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Registrar_SemIdempotencyKey_Retorna400()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(PayloadAto(tipo, SerieUnica(), "1"));

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST repetido com a mesma chave e o mesmo corpo devolve a resposta cacheada")]
    public async Task Registrar_ReplayLegitimo_DevolveCache()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        object payload = PayloadAto(tipo, SerieUnica(), "1");
        string chave = Guid.NewGuid().ToString();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage primeira = await PostAtoAsync(client, payload, chave);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        string corpoPrimeira = await primeira.Content.ReadAsStringAsync();

        HttpResponseMessage segunda = await PostAtoAsync(client, payload, chave);
        segunda.StatusCode.Should().Be(HttpStatusCode.Created);
        (await segunda.Content.ReadAsStringAsync()).Should().Be(corpoPrimeira);
    }
}
