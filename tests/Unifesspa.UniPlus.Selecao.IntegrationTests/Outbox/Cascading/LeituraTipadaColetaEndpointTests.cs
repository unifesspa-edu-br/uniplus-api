namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A leitura tipada de fatos coletados e regras de derivação no <c>GET</c> do processo, fim
/// a fim: configura a coleta e a derivação pelos endpoints de escrita e confere que a
/// consulta as devolve tipadas, com <c>exibicao</c>/<c>quando</c> ausente como <c>null</c> (nunca
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

        (await ColetarBaixaRendaAsync(ctx)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DerivarEgressoDeEscolaPublicaAsync(ctx)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument doc = await ctx.ObterProcessoAsync();
        ConferirItensColetados(FormularioNaRespostaDoProcesso.Itens(FormularioNaRespostaDoProcesso.DeInscricao(doc)).ToArray());
        ConferirRegrasDeDerivacao(doc.RootElement.GetProperty("regrasDerivacao"));
    }

    // COR_RACA vem da seção do conjunto básico, sem exibição; BAIXA_RENDA, depois dela, com exibição citando COR_RACA.
    private static Task<HttpResponseMessage> ColetarBaixaRendaAsync(Contexto ctx) =>
        ctx.PutFatosAsync(
        [
            FormularioDeInscricaoHttp.Item(
                "BAIXA_RENDA", 0, "Baixa renda", "SIM_OU_NAO", "NUNCA",
                exibicao: CorRacaPreta()),
        ]);

    // Derivação do egresso de escola pública: regra incondicional (`quando` nulo) e condicional citando `COR_RACA`.
    private static Task<HttpResponseMessage> DerivarEgressoDeEscolaPublicaAsync(Contexto ctx) =>
        ctx.PutRegrasAsync(
        [
            new { codigoFato = "EGRESSO_ESCOLA_PUBLICA", regras = new object[] { RegraDeDerivacao(0, quando: null), RegraDeDerivacao(1, CorRacaPreta()) } },
        ]);

    private static object RegraDeDerivacao(int ordem, object? quando) => new { ordem, contribui = (string?)null, quando };

    private static IReadOnlyList<IReadOnlyList<object>> CorRacaPreta() => [[new { fato = "COR_RACA", operador = "IGUAL", valor = "PRETA" }]];

    // Os itens do formulário de inscrição, ordenados por ordem; a exibição ausente é null.
    private static void ConferirItensColetados(JsonElement[] itens)
    {
        itens.Length.Should().Be(ConjuntoBasicoDaInscricao.Itens.Count + 1);
        JsonElement corRaca = itens.Single(static f => f.GetProperty("fatoCodigo").GetString() == "COR_RACA");
        corRaca.GetProperty("apresentacao").GetProperty("rotulo").GetString().Should().Be("Cor ou raça");
        corRaca.GetProperty("apresentacao").GetProperty("tipoRenderizacao").GetString().Should().Be("ESCOLHA_UNICA");
        corRaca.GetProperty("regras").GetProperty("obrigatoriedade").GetProperty("tipo").GetString().Should().Be("SEMPRE");
        corRaca.GetProperty("regras").GetProperty("exibicao").ValueKind.Should().Be(JsonValueKind.Null, "fato sem exibição é null, nunca []");
        ConferirExibicaoDaBaixaRenda(itens[ConjuntoBasicoDaInscricao.Itens.Count]);
    }

    private static void ConferirExibicaoDaBaixaRenda(JsonElement baixaRenda)
    {
        baixaRenda.GetProperty("fatoCodigo").GetString().Should().Be("BAIXA_RENDA");
        JsonElement exibicao = baixaRenda.GetProperty("regras").GetProperty("exibicao");
        exibicao.ValueKind.Should().Be(JsonValueKind.Array);
        JsonElement condicao = exibicao[0][0];
        condicao.GetProperty("fato").GetString().Should().Be("COR_RACA");
        condicao.GetProperty("operador").GetString().Should().Be("IGUAL");
        condicao.GetProperty("valor").GetString().Should().Be("PRETA");
    }

    // As regras de derivação do processo, por ordem, com a regra incondicional de `quando` nulo.
    private static void ConferirRegrasDeDerivacao(JsonElement regrasDerivacao)
    {
        regrasDerivacao.GetArrayLength().Should().Be(1);
        JsonElement config = regrasDerivacao[0];
        config.GetProperty("codigoFato").GetString().Should().Be("EGRESSO_ESCOLA_PUBLICA");
        JsonElement regras = config.GetProperty("regras");
        regras.GetArrayLength().Should().Be(2);
        regras[0].GetProperty("contribui").ValueKind.Should().Be(JsonValueKind.Null, "o derivado booleano não contribui código");
        regras[0].GetProperty("quando").ValueKind.Should().Be(JsonValueKind.Null, "a regra âncora tem quando null, nunca []");
        regras[1].GetProperty("quando").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET de um processo sem itens próprios devolve só os dados básicos e listas vazias tipadas")]
    public async Task Get_SemColeta_ListasVazias()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Get_SemColeta_ListasVazias));

        using JsonDocument doc = await ctx.ObterProcessoAsync();
        JsonElement root = doc.RootElement;

        FormularioNaRespostaDoProcesso.Itens(FormularioNaRespostaDoProcesso.DeInscricao(doc))
            .Select(static f => f.GetProperty("fatoCodigo").GetString()).Should().Equal(ConjuntoBasicoDaInscricao.Itens.Select(static i => i.FatoCodigo));
        root.GetProperty("regrasDerivacao").GetArrayLength().Should().Be(0);
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public Task<HttpResponseMessage> PutFatosAsync(IReadOnlyList<object> corpo) =>
            PutAsync(FormularioDeInscricaoHttp.RotaDosItens(ProcessoId), FormularioDeInscricaoHttp.CorpoDosItens(corpo));

        public Task<HttpResponseMessage> PutRegrasAsync(IReadOnlyList<object> corpo) =>
            PutAsync(new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/regras-derivacao", UriKind.Relative), corpo);

        private async Task<HttpResponseMessage> PutAsync(Uri rota, object corpo)
        {
            using HttpRequestMessage request = new(HttpMethod.Put, rota)
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
            UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}" });
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
