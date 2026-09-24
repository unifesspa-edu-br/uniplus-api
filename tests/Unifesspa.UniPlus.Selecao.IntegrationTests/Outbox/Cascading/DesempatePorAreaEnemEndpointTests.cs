namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O critério de desempate por área do ENEM pelo HTTP, com a regra semeada no catálogo e a
/// classificação congelando de verdade o quadro de pesos por área do cadastro: a ordem de
/// áreas é gravada por código, volta pelo GET como o PUT a recebe, e a coerência com o quadro
/// é conferida dos dois lados.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DesempatePorAreaEnemEndpointTests
{
    private const string ProcessoMediaType = "application/vnd.uniplus.processo-seletivo.v1+json";

    private readonly CascadingFixture _fixture;

    public DesempatePorAreaEnemEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A ordem de áreas do quadro congelado é gravada e volta pelo GET por código, na ordem, round-tripável")]
    public async Task AreasDoQuadro_GravaEVoltaPeloGet()
    {
        Contexto ctx = await SemearComClassificacaoEnemAsync(nameof(AreasDoQuadro_GravaEVoltaPeloGet));

        HttpResponseMessage gravacao = await ctx.PutAsync("criterios-desempate", CorpoPorArea("REDACAO", "MATEMATICA"));
        gravacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravacao.Content.ReadAsStringAsync());

        using JsonDocument doc = await ctx.ObterAsync();
        JsonElement criterio = doc.RootElement.GetProperty("criteriosDesempate")[0];
        criterio.GetProperty("regra").GetProperty("codigo").GetString().Should().Be(CriterioDesempateCodigo.MaiorNotaAreaEnem);
        string[] areas = [.. criterio.GetProperty("areas").EnumerateArray().Select(static a => a.GetString()!)];
        areas.Should().Equal("REDACAO", "MATEMATICA");

        // O rótulo não mora no critério: está no quadro da própria classificação.
        doc.RootElement.GetProperty("classificacao").GetProperty("quadroPesoAreaEnem")[0]
            .GetProperty("areas").EnumerateArray()
            .Should().Contain(area =>
                area.GetProperty("codigo").GetString() == "MATEMATICA"
                && area.GetProperty("rotulo").GetString() == "Matemática e suas Tecnologias");

        (await ctx.PutAsync("criterios-desempate", CorpoPorArea(areas)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "o que voltou é aceito de volta pelo mesmo PUT");
    }

    [Fact(DisplayName = "Área fora do quadro congelado é recusada com 422 que lista os códigos aceitos com o rótulo")]
    public async Task AreaForaDoQuadro_422ListaAsAceitas()
    {
        Contexto ctx = await SemearComClassificacaoEnemAsync(nameof(AreaForaDoQuadro_422ListaAsAceitas));

        HttpResponseMessage resposta = await ctx.PutAsync("criterios-desempate", CorpoPorArea("REDACAO", "FISICA"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        string[] textos = [.. Textos(problema.RootElement)];
        textos.Should().Contain("uniplus.selecao.processo_seletivo.desempate_area_enem_fora_do_quadro");
        textos.Should().Contain(texto => texto.Contains("FISICA", StringComparison.Ordinal)
            && texto.Contains("REDACAO (Redação)", StringComparison.Ordinal)
            && texto.Contains("MATEMATICA (Matemática e suas Tecnologias)", StringComparison.Ordinal));

        (await CriteriosGravadosAsync(ctx)).Should().BeEmpty("a recusa não grava nada");
    }

    [Fact(DisplayName = "Área repetida na ordem é recusada com 422 no campo do item")]
    public async Task AreaRepetida_422NoCampoDoItem()
    {
        Contexto ctx = await SemearComClassificacaoEnemAsync(nameof(AreaRepetida_422NoCampoDoItem));

        HttpResponseMessage resposta = await ctx.PutAsync("criterios-desempate", CorpoPorArea("REDACAO", "MATEMATICA", "REDACAO"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        string[] textos = [.. Textos(problema.RootElement)];
        textos.Should().Contain("uniplus.selecao.criterio_desempate.area_repetida");
        textos.Should().Contain("criterios[0].areas[2]");
    }

    [Fact(DisplayName = "Desmarcar ENEM na classificação havendo desempate por área é recusado com 422")]
    public async Task DesmarcarEnemComDesempatePorArea_422()
    {
        Contexto ctx = await SemearComClassificacaoEnemAsync(nameof(DesmarcarEnemComDesempatePorArea_422));
        (await ctx.PutAsync("criterios-desempate", CorpoPorArea("REDACAO"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await ctx.PutAsync("classificacao", CorpoImportadaSemEnem());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Textos(problema.RootElement).Should().Contain("uniplus.selecao.processo_seletivo.desempate_area_enem_sem_quadro");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.BaseadoEmEnem.Should().BeTrue("a recusa mantém a classificação que o processo já tinha");
    }

    [Fact(DisplayName = "Lista de critérios acima do teto é recusada com 422 de um erro só, com o código mapeado do agregado")]
    public async Task AcimaDoTeto_422ComUmErroMapeado()
    {
        Contexto ctx = await SemearComClassificacaoEnemAsync(nameof(AcimaDoTeto_422ComUmErroMapeado));
        object[] criterios = [.. Enumerable.Range(1, 10_000).Select(static ordem => (object)new
        {
            ordem,
            regraCodigo = string.Empty,
            regraVersao = string.Empty,
        })];

        HttpResponseMessage resposta = await ctx.PutAsync("criterios-desempate", criterios);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.processo_seletivo.criterios_desempate_em_excesso");
        if (problema.RootElement.TryGetProperty("errors", out JsonElement errors))
        {
            errors.GetArrayLength().Should().BeLessThanOrEqualTo(1);
        }
    }

    [Fact(DisplayName = "Desempate por área sob classificação que não é ENEM é recusado com 422")]
    public async Task ClassificacaoSemEnem_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ClassificacaoSemEnem_422));

        HttpResponseMessage resposta = await ctx.PutAsync("criterios-desempate", CorpoPorArea("REDACAO"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Textos(problema.RootElement).Should().Contain("uniplus.selecao.processo_seletivo.desempate_area_enem_sem_quadro");
    }

    private static object[] CorpoPorArea(params string[] areas) =>
    [
        new
        {
            ordem = 1,
            regraCodigo = CriterioDesempateCodigo.MaiorNotaAreaEnem,
            regraVersao = "v1",
            areas,
        },
    ];

    private static object CorpoEnemLocal(string resolucao) => new
    {
        regraCalculoCodigo = RegraCalculoCodigo.FormulaMediaPonderada,
        regraCalculoVersao = "v1",
        regraArredondamentoCodigo = RegraArredondamentoCodigo.PrecisaoTruncar,
        regraArredondamentoVersao = "v1",
        casasArredondamento = 2,
        regraOrdemAlocacaoCodigo = RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria,
        regraOrdemAlocacaoVersao = "v1",
        nOpcoesAlocacao = 1,
        regrasEliminacao = Array.Empty<object>(),
        baseadoEmEnem = true,
        resolucaoPesoAreaEnem = resolucao,
    };

    private static object CorpoImportadaSemEnem() => new
    {
        regraCalculoCodigo = RegraCalculoCodigo.ClassificacaoImportada,
        regraCalculoVersao = "v1",
        regraArredondamentoCodigo = (string?)null,
        regraArredondamentoVersao = (string?)null,
        casasArredondamento = (int?)null,
        regraOrdemAlocacaoCodigo = RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria,
        regraOrdemAlocacaoVersao = "v1",
        nOpcoesAlocacao = 1,
        regrasEliminacao = Array.Empty<object>(),
        baseadoEmEnem = false,
    };

    private static IEnumerable<string> Textos(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.String => [elemento.GetString()!],
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(propriedade => Textos(propriedade.Value)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(Textos),
        _ => [],
    };

    private static async Task<List<CriterioDesempate>> CriteriosGravadosAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.Set<CriterioDesempate>().AsNoTracking()
            .Where(c => c.ProcessoSeletivoId == ctx.ProcessoId)
            .ToListAsync();
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<HttpResponseMessage> PutAsync(string recurso, object corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/{recurso}", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<JsonDocument> ObterAsync()
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

    private async Task<Contexto> SemearComClassificacaoEnemAsync(string nome)
    {
        Contexto ctx = await SemearRascunhoAsync(nome);
        string resolucao = await PesosAreaEnemSeeder.SemearResolucaoAsync(ctx.Api);

        HttpResponseMessage classificacao = await ctx.PutAsync("classificacao", CorpoEnemLocal(resolucao));
        classificacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await classificacao.Content.ReadAsStringAsync());

        return ctx;
    }

    private async Task<Contexto> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
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
}
