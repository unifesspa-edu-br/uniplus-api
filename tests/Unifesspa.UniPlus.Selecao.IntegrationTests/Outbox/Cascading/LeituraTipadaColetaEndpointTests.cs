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
/// A leitura tipada de fatos coletados e regras de derivação no <c>GET</c> do processo (Story
/// #987), fim a fim: configura a coleta e a derivação pelos endpoints de escrita e confere que a
/// consulta as devolve tipadas, com <c>precondicao</c>/<c>quando</c> ausente como <c>null</c> (nunca
/// <c>[]</c>) e o valor tipado — round-trip com a forma de escrita.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class LeituraTipadaColetaEndpointTests
{
    private const string ProcessoMediaType = "application/vnd.uniplus.processo-seletivo.v1+json";

    private readonly CascadingFixture _fixture;

    public LeituraTipadaColetaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "GET do processo devolve fatos coletados e regras de derivação tipados, com ausência como null")]
    public async Task Get_ProjetaColetaEDerivacaoTipadas()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Get_ProjetaColetaEDerivacaoTipadas));

        // Coleta: COR_RACA (ordem 0, sem pré-condição); BAIXA_RENDA (ordem 1, com pré-condição citando o anterior).
        (await ctx.PutFatosAsync(
        [
            new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null },
            new
            {
                fatoCodigo = "BAIXA_RENDA",
                ordem = 1,
                precondicao = new[] { new[] { new { fato = "COR_RACA", operador = "IGUAL", valor = "PRETA" } } },
            },
        ])).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Derivação de MODALIDADE: regra âncora (quando null) + condicional citando COR_RACA. Ambas contribuem AC (única modalidade ofertada pelo seeder).
        (await ctx.PutRegrasAsync(
        [
            new
            {
                codigoFato = "MODALIDADE",
                regras = new object[]
                {
                    new { ordem = 0, contribui = "AC", quando = (object?)null },
                    new
                    {
                        ordem = 1,
                        contribui = "AC",
                        quando = new[] { new[] { new { fato = "COR_RACA", operador = "IGUAL", valor = "PRETA" } } },
                    },
                },
            },
        ])).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument doc = await ctx.ObterProcessoAsync();
        JsonElement root = doc.RootElement;

        // Fatos coletados — ordenados por ordem, precondicao ausente é null.
        JsonElement fatos = root.GetProperty("fatosColetados");
        fatos.GetArrayLength().Should().Be(2);

        JsonElement corRaca = fatos[0];
        corRaca.GetProperty("fatoCodigo").GetString().Should().Be("COR_RACA");
        corRaca.GetProperty("precondicao").ValueKind.Should().Be(JsonValueKind.Null, "fato sem pré-condição é null, nunca []");

        JsonElement baixaRenda = fatos[1];
        baixaRenda.GetProperty("fatoCodigo").GetString().Should().Be("BAIXA_RENDA");
        JsonElement precondicao = baixaRenda.GetProperty("precondicao");
        precondicao.ValueKind.Should().Be(JsonValueKind.Array);
        JsonElement condicao = precondicao[0][0];
        condicao.GetProperty("fato").GetString().Should().Be("COR_RACA");
        condicao.GetProperty("operador").GetString().Should().Be("IGUAL");
        condicao.GetProperty("valor").GetString().Should().Be("PRETA");

        // Regras de derivação — MODALIDADE, regras por ordem, âncora com quando null.
        JsonElement regrasDerivacao = root.GetProperty("regrasDerivacao");
        regrasDerivacao.GetArrayLength().Should().Be(1);
        JsonElement config = regrasDerivacao[0];
        config.GetProperty("codigoFato").GetString().Should().Be("MODALIDADE");
        JsonElement regras = config.GetProperty("regras");
        regras.GetArrayLength().Should().Be(2);
        regras[0].GetProperty("contribui").GetString().Should().Be("AC");
        regras[0].GetProperty("quando").ValueKind.Should().Be(JsonValueKind.Null, "a regra âncora tem quando null, nunca []");
        regras[1].GetProperty("quando").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET de um processo sem coleta devolve listas vazias tipadas")]
    public async Task Get_SemColeta_ListasVazias()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Get_SemColeta_ListasVazias));

        using JsonDocument doc = await ctx.ObterProcessoAsync();
        JsonElement root = doc.RootElement;

        root.GetProperty("fatosColetados").GetArrayLength().Should().Be(0);
        root.GetProperty("regrasDerivacao").GetArrayLength().Should().Be(0);
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public Task<HttpResponseMessage> PutFatosAsync(IReadOnlyList<object> corpo) =>
            PutAsync("fatos-coletados", corpo);

        public Task<HttpResponseMessage> PutRegrasAsync(IReadOnlyList<object> corpo) =>
            PutAsync("regras-derivacao", corpo);

        private async Task<HttpResponseMessage> PutAsync(string recurso, object corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/{recurso}", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<JsonDocument> ObterProcessoAsync()
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}", UriKind.Relative));
            Autenticar(request);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(ProcessoMediaType));
            HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
    }

    private async Task<Contexto> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();

        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
        }

        return new Contexto(api, client, processoId);
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
