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
/// O endpoint <c>PUT /processos-seletivos/{id}/fatos-coletados</c> (Story #984), fim a fim
/// pelo HTTP. Prova o que só o ciclo real prova: a coletabilidade e o vocabulário resolvidos
/// contra o seed cross-módulo de Configuração, o guard de rascunho num processo publicado, a
/// idempotência do replay e a autorização herdada do controller.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirFatosColetadosEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirFatosColetadosEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Coleta válida em rascunho é aceita com 204 sem ETag e persiste os fatos")]
    public async Task Rascunho_ColetaValida_204SemEtag()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_ColetaValida_204SemEtag));

        object[] corpo =
        [
            new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null },
            new
            {
                fatoCodigo = "BAIXA_RENDA",
                ordem = 1,
                precondicao = new[] { new[] { new { fato = "COR_RACA", operador = "IGUAL", valor = "PRETA" } } },
            },
        ];

        HttpResponseMessage resposta = await ctx.PutFatosAsync(corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resposta.Headers.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<FatoColetado> fatos = await db.Set<FatoColetado>().AsNoTracking()
            .Where(f => f.ProcessoSeletivoId == ctx.ProcessoId).ToListAsync();
        fatos.Select(f => f.FatoCodigo).Should().BeEquivalentTo(["COR_RACA", "BAIXA_RENDA"]);
    }

    [Fact(DisplayName = "Fato fora do vocabulário é recusado com 422 sem tradução")]
    public async Task Rascunho_FatoDesconhecido_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_FatoDesconhecido_422));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [new { fatoCodigo = "V", ordem = 0, precondicao = (object?)null }]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Coletar um fato derivado (MODALIDADE) é recusado com 422")]
    public async Task Rascunho_FatoDerivado_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_FatoDerivado_422));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [new { fatoCodigo = "MODALIDADE", ordem = 0, precondicao = (object?)null }]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Redefinir a coleta de um processo publicado é recusado com 422 (só em rascunho)")]
    public async Task Publicado_Recusa_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Publicado_Recusa_422));
        await ctx.PublicarAsync();

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null }]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Replay com a mesma Idempotency-Key e o mesmo corpo devolve o mesmo 204")]
    public async Task Rascunho_ReplayIdempotente_204()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_ReplayIdempotente_204));
        object[] corpo = [new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null }];
        string chave = MakeIdempotencyKey();

        (await ctx.PutFatosAsync(corpo, idempotencyKey: chave)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage replay = await ctx.PutFatosAsync(corpo, idempotencyKey: chave);

        replay.StatusCode.Should().Be(HttpStatusCode.NoContent);
        replay.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
    }

    [Fact(DisplayName = "Sem autenticação é 401")]
    public async Task SemAutenticacao_401()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemAutenticacao_401));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null }], autenticar: Autenticacao.Nenhuma);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Autenticado sem o papel plataforma-admin é 403")]
    public async Task SemPapel_403()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemPapel_403));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null }], autenticar: Autenticacao.PapelErrado);

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
        public async Task<HttpResponseMessage> PutFatosAsync(
            IReadOnlyList<object> corpo, string? idempotencyKey = null, Autenticacao autenticar = Autenticacao.PlataformaAdmin)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/fatos-coletados", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request, autenticar);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? MakeIdempotencyKey());
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
