namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/taxa-inscricao</c>, fim a fim pelo HTTP —
/// prova a obrigatoriedade real de <c>Cobra</c> na desserialização (mesmo raciocínio de
/// <see cref="DefinirClassificacaoEndpointTests"/>, issue #850): o host não habilita
/// <c>RespectRequiredConstructorParameters</c>, então por padrão do <c>System.Text.Json</c>
/// um parâmetro de construtor ausente no corpo recebe o valor default (<see langword="null"/>)
/// — indistinguível de um <c>cobra: null</c> explícito, que o handler interpreta como remoção
/// deliberada da declaração de taxa (CA-01). <c>[property: JsonRequired]</c> em
/// <c>DefinirTaxaInscricaoRequest</c> fecha essa lacuna: omitir o campo vira <c>400</c>, nunca
/// uma remoção silenciosa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirTaxaInscricaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirTaxaInscricaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Corpo sem cobra é rejeitado com 400 — não vira remoção silenciosa da declaração")]
    public async Task SemCobra_400()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemCobra_400));

        const string corpoSemCampo = """
            {
              "valor": 100.00,
              "fundamentos": null,
              "confirmacaoFundamentos": false
            }
            """;

        HttpResponseMessage resposta = await ctx.PutTaxaInscricaoRawAsync(corpoSemCampo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "cobra omitido não pode desserializar como null implícito — o parâmetro é [JsonRequired]");
    }

    [Fact(DisplayName = "cobra=null explícito é aceito e remove a declaração existente (CA-01, toggle deliberado)")]
    public async Task ComCobraNuloExplicito_204ERemoveDeclaracao()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComCobraNuloExplicito_204ERemoveDeclaracao));

        HttpResponseMessage primeira = await ctx.PutTaxaInscricaoAsync(CorpoComCobranca(cobra: true, valor: 100m));
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await ctx.PutTaxaInscricaoRawAsync(
            """{ "cobra": null, "valor": null, "fundamentos": null, "confirmacaoFundamentos": false }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        bool existe = await db.Set<ConfiguracaoTaxaInscricao>().AsNoTracking()
            .AnyAsync(t => t.ProcessoSeletivoId == ctx.ProcessoId);
        existe.Should().BeFalse("cobra:null explícito remove deliberadamente a declaração existente");
    }

    [Fact(DisplayName = "cobra=false explícito é aceito e persiste a declaração de não cobrança")]
    public async Task ComCobraFalseExplicito_204EPersisteNaoCobra()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComCobraFalseExplicito_204EPersisteNaoCobra));

        HttpResponseMessage resposta = await ctx.PutTaxaInscricaoAsync(CorpoComCobranca(cobra: false, valor: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoTaxaInscricao taxa = await db.Set<ConfiguracaoTaxaInscricao>().AsNoTracking()
            .SingleAsync(t => t.ProcessoSeletivoId == ctx.ProcessoId);
        taxa.Cobra.Should().BeFalse();
    }

    // Issue #1310: quem cobra declara ao menos um fundamento de isenção — o corpo de sucesso
    // do ramo "cobra" carrega o fundamento e a confirmação que a fábrica exige.
    [Fact(DisplayName = "cobra=true sem fundamento de isenção é rejeitado com 422 nomeado (issue #1310)")]
    public async Task ComCobraSemFundamento_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComCobraSemFundamento_422));

        HttpResponseMessage resposta = await ctx.PutTaxaInscricaoRawAsync(
            """{ "cobra": true, "valor": 100.00, "fundamentos": [], "confirmacaoFundamentos": false }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "quem cobra reconhece ao menos um fundamento — a possibilidade de pedir isenção se " +
            "materializa nos fundamentos declarados pelo processo");

        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.selecao.configuracao_taxa_inscricao.fundamento_obrigatorio_quando_cobra");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoTaxaInscricao taxa = await db.Set<ConfiguracaoTaxaInscricao>().AsNoTracking()
            .SingleAsync(t => t.ProcessoSeletivoId == ctx.ProcessoId);
        taxa.Cobra.Should().BeFalse(
            "a recusa precede a escrita — a declaração de não cobrança que a semeadura gravou " +
            "continua intacta, e não virou uma declaração de cobrança sem fundamento");
    }

    [Fact(DisplayName = "cobra=true com fundamento e confirmação é aceito e persiste o fundamento (issue #1310)")]
    public async Task ComCobraEFundamento_204EPersisteFundamento()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComCobraEFundamento_204EPersisteFundamento));

        HttpResponseMessage resposta = await ctx.PutTaxaInscricaoAsync(CorpoComCobranca(cobra: true, valor: 100m));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoTaxaInscricao taxa = await db.Set<ConfiguracaoTaxaInscricao>().AsNoTracking()
            .SingleAsync(t => t.ProcessoSeletivoId == ctx.ProcessoId);
        taxa.Cobra.Should().BeTrue();
        taxa.Fundamentos.Should().Equal(FundamentoIsencao.CadastroUnico);
    }

    private static object CorpoComCobranca(bool cobra, decimal? valor) => new
    {
        cobra,
        valor,
        fundamentos = cobra ? [FundamentoIsencaoCodigo.CadastroUnico] : (string[]?)null,
        confirmacaoFundamentos = cobra,
    };

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<HttpResponseMessage> PutTaxaInscricaoAsync(object corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/taxa-inscricao", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> PutTaxaInscricaoRawAsync(string corpoJson)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/taxa-inscricao", UriKind.Relative))
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
