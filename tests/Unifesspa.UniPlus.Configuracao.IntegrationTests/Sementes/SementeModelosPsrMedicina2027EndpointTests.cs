namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Os modelos de inscrição e de habilitação do edital de Medicina 2027 existem ao subir o sistema,
/// gravados pela migration <c>SemeiaModelosPsrMedicina2027</c>, e a API os aceita como se tivessem
/// sido cadastrados pela tela.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class SementeModelosPsrMedicina2027EndpointTests
{
    private const string Base = "/api/configuracao/admin/modelos-formulario";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public SementeModelosPsrMedicina2027EndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "O modelo semeado está ativo, tem ajuda em todo campo, e o conteúdo dele volta à escrita aceito pela conferência do catálogo")]
    [InlineData(SementePsrMedicina2027.ModeloDeInscricao)]
    [InlineData(SementePsrMedicina2027.ModeloDeHabilitacao)]
    public async Task ModeloSemeado_EhAceitoPelaEscrita(string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = await ObterPeloCodigoAsync(client, codigo);

        modelo["ativo"]!.GetValue<bool>().Should().BeTrue();
        modelo["tipoProcessoCodigo"]!.GetValue<string>().Should().Be(SementePsrMedicina2027.TipoProcessoCodigo);
        JsonNode conteudo = modelo["conteudo"]!;
        IEnumerable<JsonNode> campos = conteudo["itens"]!.AsArray()
            .Concat(conteudo["grupos"]!.AsArray().SelectMany(static g => g!["subitens"]!.AsArray()))!;
        campos.Should().NotBeEmpty().And.OnlyContain(static c => !string.IsNullOrWhiteSpace(c["ajuda"]!.GetValue<string>()));

        // A escrita confere o conteúdo contra o catálogo de fatos, os termos e o tipo de processo,
        // o que a migration não alcança: aceito aqui, o modelo é o que a tela teria gravado.
        HttpResponseMessage reescrita = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{modelo["id"]!.GetValue<Guid>()}", new
        {
            nome = modelo["nome"]!.GetValue<string>(),
            descricao = modelo["descricao"]!.GetValue<string>(),
            tipoProcessoCodigo = SementePsrMedicina2027.TipoProcessoCodigo,
            conteudo,
        });
        reescrita.StatusCode.Should().Be(HttpStatusCode.NoContent, await reescrita.Content.ReadAsStringAsync());
    }

    [Theory(DisplayName = "A pré-visualização do modelo semeado responde")]
    [InlineData(SementePsrMedicina2027.ModeloDeInscricao)]
    [InlineData(SementePsrMedicina2027.ModeloDeHabilitacao)]
    public async Task ModeloSemeado_PreVisualiza(string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = await ObterPeloCodigoAsync(client, codigo);

        HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, $"{Base}/{modelo["id"]!.GetValue<Guid>()}/pre-visualizacao",
            new { respostas = new Dictionary<string, object>() });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<JsonObject> ObterPeloCodigoAsync(HttpClient client, string codigo)
    {
        HttpResponseMessage lista = await EnviarAsync(client, HttpMethod.Get, $"{Base}?tipoProcesso={SementePsrMedicina2027.TipoProcessoCodigo}", corpo: null);
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        Guid id = JsonNode.Parse(await lista.Content.ReadAsStringAsync())!.AsArray()
            .Single(m => m!["codigo"]!.GetValue<string>() == codigo)!["id"]!.GetValue<Guid>();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!.AsObject();
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        return await client.SendAsync(request);
    }
}
