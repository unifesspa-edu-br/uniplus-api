namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O valor de uma condição sobre fato do candidato viaja no MESMO formato nas duas leituras
/// que o carregam — o gatilho da exigência documental e o critério de desempate.
/// </summary>
/// <remarks>
/// Enquanto o critério de desempate desachatava a string, um valor de texto <c>"18"</c> voltava
/// como <c>18</c> e era reinterpretado como número na gravação seguinte, mudando qual ramo da
/// matriz operador × domínio o validaria. E obrigava o cliente a conhecer duas convenções para
/// o mesmo campo — que foi como a tela de desempate passou a ler em branco um critério
/// perfeitamente gravado.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ValorDeCondicaoNoGetTests
{
    private const string ProcessoMediaType = "application/vnd.uniplus.processo-seletivo.v1+json";

    private readonly CascadingFixture _fixture;

    public ValorDeCondicaoNoGetTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O valor categórico do critério de desempate volta como texto JSON, round-tripável pelo mesmo PUT")]
    public async Task Get_ValorCategorico_VoltaComoTextoJson()
    {
        (HttpClient client, Guid processoId) = await SemearAsync();

        object[] criterios =
        [
            new
            {
                ordem = 1,
                regraCodigo = "DESEMPATE-PREDICADO-FATO",
                regraVersao = "v1",
                fato = "COR_RACA",
                operador = "IGUAL",
                valor = "PRETA",
            },
        ];

        HttpResponseMessage gravacao = await PutAsync(client, processoId, "criterios-desempate", criterios);
        gravacao.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            await gravacao.Content.ReadAsStringAsync());

        using JsonDocument doc = await ObterAsync(client, processoId);
        JsonElement criterio = doc.RootElement.GetProperty("criteriosDesempate")[0];

        criterio.GetProperty("valor").GetString().Should().Be(
            "\"PRETA\"", "é o mesmo texto JSON canônico que o gatilho da exigência documental usa");

        // Round-trip: o que voltou é aceito de volta sem transformação do cliente.
        object[] devolta =
        [
            new
            {
                ordem = 1,
                regraCodigo = "DESEMPATE-PREDICADO-FATO",
                regraVersao = "v1",
                fato = criterio.GetProperty("fato").GetString(),
                operador = criterio.GetProperty("operador").GetString(),
                valor = criterio.GetProperty("valor").GetString(),
            },
        ];

        (await PutAsync(client, processoId, "criterios-desempate", devolta))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<HttpResponseMessage> PutAsync(
        HttpClient client, Guid processoId, string recurso, object corpo)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/{recurso}", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<JsonDocument> ObterAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            new Uri($"/api/selecao/processos-seletivos/{processoId}", UriKind.Relative));
        Autenticar(request);
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(ProcessoMediaType));
        HttpResponseMessage resposta = await client.SendAsync(request).ConfigureAwait(false);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    private async Task<(HttpClient Client, Guid ProcessoId)> SemearAsync()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);

        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"Valor de condição {Guid.CreateVersion7()}");
            processoId = processo.Id;
        }

        return (api.CreateClient(), processoId);
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
