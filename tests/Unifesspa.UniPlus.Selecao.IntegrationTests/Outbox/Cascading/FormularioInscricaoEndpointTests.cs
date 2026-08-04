namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O formulário de inscrição (Story #559), fim a fim pelo HTTP: leitura pública sob
/// <c>/processos-seletivos/{id}/formulario</c> e escrita administrativa sob
/// <c>/admin/processos-seletivos/{id}/formulario</c> — mesmo padrão path-based (ADR-0064) de
/// <see cref="ObrigatoriedadeLegalController"/>, agora sobre <c>ProcessoSeletivo</c>.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FormularioInscricaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FormularioInscricaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "PUT admin sem autenticação é 401")]
    public async Task Definir_SemAutenticacao_401()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_SemAutenticacao_401));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Título", "Termo", autenticar: Autenticacao.Nenhuma);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "PUT admin autenticado sem o papel plataforma-admin é 403")]
    public async Task Definir_SemPapel_403()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_SemPapel_403));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Título", "Termo", autenticar: Autenticacao.PapelErrado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "PUT admin em rascunho é 204 sem ETag e persiste título/termo")]
    public async Task Definir_EmRascunho_204SemEtagEPersiste()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_EmRascunho_204SemEtagEPersiste));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Formulário de Inscrição", "Declaro que as informações são verdadeiras.");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resposta.Headers.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.Set<ProcessoSeletivo>().AsNoTracking()
            .SingleAsync(p => p.Id == ctx.ProcessoId);
        processo.FormularioTitulo.Should().Be("Formulário de Inscrição");
        processo.FormularioTermoAceiteTexto.Should().Be("Declaro que as informações são verdadeiras.");
    }

    [Fact(DisplayName = "GET público, sem autenticação, retorna 422 Snapshot.VigenteAusente para processo em rascunho")]
    public async Task Obter_ProcessoEmRascunho_422SnapshotVigenteAusente()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Obter_ProcessoEmRascunho_422SnapshotVigenteAusente));

        HttpResponseMessage resposta = await ctx.GetFormularioAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "GET público, sem autenticação, retorna 404 para processo inexistente")]
    public async Task Obter_ProcessoInexistente_404()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Obter_ProcessoInexistente_404));

        HttpResponseMessage resposta = await ctx.Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{Guid.NewGuid()}/formulario", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET público, sem autenticação, retorna 200 com título/termo/fatos para processo publicado")]
    public async Task Obter_ProcessoPublicado_200ComFormularioEFatos()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Obter_ProcessoPublicado_200ComFormularioEFatos));

        (await ctx.PutFormularioAsync("Formulário de Inscrição", "Declaro que as informações são verdadeiras."))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.PutFatosAsync(
        [
            new { fatoCodigo = "COR_RACA", ordem = 0, rotulo = "Cor ou raça", tipoRenderizacao = "SELECAO_UNICA", obrigatorio = true, precondicao = (object?)null },
        ])).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ctx.PublicarAsync();

        HttpResponseMessage resposta = await ctx.GetFormularioAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("titulo").GetString().Should().Be("Formulário de Inscrição");
        root.GetProperty("termoAceiteTexto").GetString().Should().Be("Declaro que as informações são verdadeiras.");
        JsonElement fatos = root.GetProperty("fatosColetados");
        fatos.GetArrayLength().Should().Be(1);
        JsonElement fato = fatos[0];
        fato.GetProperty("fatoCodigo").GetString().Should().Be("COR_RACA");
        fato.GetProperty("rotulo").GetString().Should().Be("Cor ou raça");
        fato.GetProperty("tipoRenderizacao").GetString().Should().Be("SELECAO_UNICA");
        fato.GetProperty("obrigatorio").GetBoolean().Should().BeTrue();
    }

    private enum Autenticacao
    {
        PlataformaAdmin,
        PapelErrado,
        Nenhuma,
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid DocumentoId)
    {
        public async Task<HttpResponseMessage> PutFormularioAsync(
            string? titulo, string? termoAceiteTexto, Autenticacao autenticar = Autenticacao.PlataformaAdmin)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formulario", UriKind.Relative))
            {
                Content = JsonContent.Create(new { titulo, termoAceiteTexto }),
            };
            Autenticar(request, autenticar);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> GetFormularioAsync() => await Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/formulario", UriKind.Relative)).ConfigureAwait(false);

        public async Task<HttpResponseMessage> PutFatosAsync(IReadOnlyList<object> corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/fatos-coletados", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request, Autenticacao.PlataformaAdmin);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
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
