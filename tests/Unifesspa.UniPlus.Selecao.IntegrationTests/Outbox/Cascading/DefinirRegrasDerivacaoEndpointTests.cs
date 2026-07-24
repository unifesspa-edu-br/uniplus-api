namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/regras-derivacao</c> (Story #985), fim a fim pelo
/// HTTP. Prova o alvo derivado, a validação do <c>contribui</c> contra as modalidades ofertadas
/// pelo processo, a composição com a coleta de fatos (a condição cita um fato coletado), o guard de
/// rascunho e a autorização.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirRegrasDerivacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirRegrasDerivacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Derivação de MODALIDADE válida (âncora + condicional citando fato coletado) é aceita com 204 e persiste")]
    public async Task Rascunho_DerivacaoValida_204()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_DerivacaoValida_204));

        // A condição cita COR_RACA — precisa estar coletado no processo.
        (await ctx.PutFatosAsync([new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null }]))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        object[] corpo =
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
        ];

        HttpResponseMessage resposta = await ctx.PutRegrasAsync(corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resposta.Headers.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<ConfiguracaoDerivacaoFato> configs = await db.Set<ConfiguracaoDerivacaoFato>().AsNoTracking()
            .Where(c => c.ProcessoSeletivoId == ctx.ProcessoId).ToListAsync();
        configs.Select(c => c.CodigoFato).Should().BeEquivalentTo(["MODALIDADE"]);
    }

    [Fact(DisplayName = "Contribui fora das modalidades ofertadas é recusado com 422")]
    public async Task Rascunho_ContribuiForaDoDominio_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_ContribuiForaDoDominio_422));

        object[] corpo =
        [
            new
            {
                codigoFato = "MODALIDADE",
                regras = new object[] { new { ordem = 0, contribui = "V", quando = (object?)null } },
            },
        ];

        HttpResponseMessage resposta = await ctx.PutRegrasAsync(corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Alvo declarado (não derivável) é recusado com 422")]
    public async Task Rascunho_AlvoNaoDerivavel_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_AlvoNaoDerivavel_422));

        object[] corpo =
        [
            new
            {
                codigoFato = "COR_RACA",
                regras = new object[] { new { ordem = 0, contribui = "PRETA", quando = (object?)null } },
            },
        ];

        HttpResponseMessage resposta = await ctx.PutRegrasAsync(corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Redefinir as regras de um processo publicado é recusado com 422 (só em rascunho)")]
    public async Task Publicado_Recusa_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Publicado_Recusa_422));
        await ctx.PublicarAsync();

        object[] corpo =
        [
            new
            {
                codigoFato = "MODALIDADE",
                regras = new object[] { new { ordem = 0, contribui = "AC", quando = (object?)null } },
            },
        ];

        HttpResponseMessage resposta = await ctx.PutRegrasAsync(corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Replay com a mesma Idempotency-Key e o mesmo corpo devolve o mesmo 204")]
    public async Task Rascunho_ReplayIdempotente_204()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_ReplayIdempotente_204));
        object[] corpo =
        [
            new
            {
                codigoFato = "MODALIDADE",
                regras = new object[] { new { ordem = 0, contribui = "AC", quando = (object?)null } },
            },
        ];
        string chave = MakeIdempotencyKey();

        (await ctx.PutRegrasAsync(corpo, idempotencyKey: chave)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage replay = await ctx.PutRegrasAsync(corpo, idempotencyKey: chave);

        replay.StatusCode.Should().Be(HttpStatusCode.NoContent);
        replay.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
    }

    [Fact(DisplayName = "Sem autenticação é 401")]
    public async Task SemAutenticacao_401()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemAutenticacao_401));

        HttpResponseMessage resposta = await ctx.PutRegrasAsync(
            [new { codigoFato = "MODALIDADE", regras = new object[] { new { ordem = 0, contribui = "AC", quando = (object?)null } } }],
            autenticar: Autenticacao.Nenhuma);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Autenticado sem o papel plataforma-admin é 403")]
    public async Task SemPapel_403()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemPapel_403));

        HttpResponseMessage resposta = await ctx.PutRegrasAsync(
            [new { codigoFato = "MODALIDADE", regras = new object[] { new { ordem = 0, contribui = "AC", quando = (object?)null } } }],
            autenticar: Autenticacao.PapelErrado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private enum Autenticacao
    {
        PlataformaAdmin,
        PapelErrado,
        Nenhuma,
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid DocumentoId)
    {
        public Task<HttpResponseMessage> PutFatosAsync(IReadOnlyList<object> corpo) =>
            EnviarAsync(HttpMethod.Put, "fatos-coletados", corpo, MakeIdempotencyKey(), Autenticacao.PlataformaAdmin);

        public Task<HttpResponseMessage> PutRegrasAsync(
            IReadOnlyList<object> corpo, string? idempotencyKey = null, Autenticacao autenticar = Autenticacao.PlataformaAdmin) =>
            EnviarAsync(HttpMethod.Put, "regras-derivacao", corpo, idempotencyKey ?? MakeIdempotencyKey(), autenticar);

        private async Task<HttpResponseMessage> EnviarAsync(
            HttpMethod metodo, string recurso, object corpo, string idempotencyKey, Autenticacao autenticar)
        {
            using HttpRequestMessage request = new(
                metodo,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/{recurso}", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request, autenticar);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task PublicarAsync()
        {
            using HttpRequestMessage publicar = new(
                HttpMethod.Post,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/publicacao", UriKind.Relative))
            {
                Content = JsonContent.Create(new
                {
                    numero = "001/2026",
                    periodoInscricaoInicio = Hoje(),
                    periodoInscricaoFim = HojeMais(30),
                    documentoEditalId = DocumentoId,
                    ato = new
                    {
                        orgao = "CEPS",
                        serie = "EDITAL",
                        ano = 2026,
                        dataPublicacao = Hoje(),
                        assinante = "Diretor do CEPS",
                        tipoAtoCodigo = "EDITAL_ABERTURA",
                    },
                }),
            };
            Autenticar(publicar, Autenticacao.PlataformaAdmin);
            publicar.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            HttpResponseMessage resposta = await Client.SendAsync(publicar).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, "o cenário depende de um certame publicado");
        }
    }

    private async Task<Contexto> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        return new Contexto(api, client, processoId, documentoId);
    }

    private static void Autenticar(HttpRequestMessage request, Autenticacao autenticacao)
    {
        if (autenticacao == Autenticacao.Nenhuma)
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(
            TestAuthHandler.RolesHeader,
            autenticacao == Autenticacao.PlataformaAdmin ? "plataforma-admin" : "consulta-publica");
    }

    private static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string HojeMais(int dias) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
