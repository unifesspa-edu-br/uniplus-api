namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/classificacao</c>, fim a fim pelo HTTP —
/// prova a obrigatoriedade real de <c>BaseadoEmEnem</c> na desserialização (issue #850,
/// CA-10): o host não habilita <c>RespectRequiredConstructorParameters</c>
/// (<c>Program.cs</c> só configura naming policy e enum converter), então por padrão do
/// <c>System.Text.Json</c> um parâmetro de construtor de record ausente no corpo recebe o
/// valor default (<see langword="false"/>) — indistinguível de um <see langword="false"/>
/// explícito. <c>[property: JsonRequired]</c> em <c>DefinirClassificacaoRequest</c> fecha
/// essa lacuna: omitir o campo vira <c>400</c>, nunca um <c>false</c> silencioso.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirClassificacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirClassificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Corpo sem baseadoEmEnem é rejeitado com 400 — não vira false silencioso")]
    public async Task SemBaseadoEmEnem_400()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemBaseadoEmEnem_400));

        const string corpoSemCampo = """
            {
              "regraCalculoCodigo": "CLASSIFICACAO-IMPORTADA",
              "regraCalculoVersao": "v1",
              "regraArredondamentoCodigo": null,
              "regraArredondamentoVersao": null,
              "casasArredondamento": null,
              "regraOrdemAlocacaoCodigo": "ALOCACAO-OPCOES-RN04",
              "regraOrdemAlocacaoVersao": "v1",
              "nOpcoesAlocacao": 1,
              "regrasEliminacao": []
            }
            """;

        HttpResponseMessage resposta = await ctx.PutClassificacaoRawAsync(corpoSemCampo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "baseadoEmEnem omitido não pode desserializar como false implícito — o parâmetro é [JsonRequired]");
    }

    [Fact(DisplayName = "baseadoEmEnem=false explícito é aceito e persiste false")]
    public async Task ComBaseadoEmEnemFalseExplicito_204EPersisteFalse()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComBaseadoEmEnemFalseExplicito_204EPersisteFalse));

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoImportadaSemEliminacao(baseadoEmEnem: false));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.BaseadoEmEnem.Should().BeFalse();
    }

    [Fact(DisplayName = "baseadoEmEnem=true explícito é aceito e persiste true")]
    public async Task ComBaseadoEmEnemTrueExplicito_204EPersisteTrue()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComBaseadoEmEnemTrueExplicito_204EPersisteTrue));

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoImportadaSemEliminacao(baseadoEmEnem: true));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.BaseadoEmEnem.Should().BeTrue();
    }

    private static object CorpoImportadaSemEliminacao(bool baseadoEmEnem) => new
    {
        regraCalculoCodigo = "CLASSIFICACAO-IMPORTADA",
        regraCalculoVersao = "v1",
        regraArredondamentoCodigo = (string?)null,
        regraArredondamentoVersao = (string?)null,
        casasArredondamento = (int?)null,
        regraOrdemAlocacaoCodigo = "ALOCACAO-OPCOES-RN04",
        regraOrdemAlocacaoVersao = "v1",
        nOpcoesAlocacao = 1,
        regrasEliminacao = Array.Empty<object>(),
        baseadoEmEnem,
    };

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<HttpResponseMessage> PutClassificacaoAsync(object corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/classificacao", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> PutClassificacaoRawAsync(string corpoJson)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/classificacao", UriKind.Relative))
            {
                Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }
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

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
