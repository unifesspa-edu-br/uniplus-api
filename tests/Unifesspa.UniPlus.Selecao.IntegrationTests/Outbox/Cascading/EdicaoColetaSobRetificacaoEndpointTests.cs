namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
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
/// Edição de fatos coletados e regras de derivação <b>sob sessão de retificação</b> (Story #986),
/// fim a fim pelo HTTP: o <c>If-Match</c> passa a ser exigido (428/412/curinga), a revisão avança,
/// e o descarte repõe fielmente a coleta congelada — inclusive quando a sessão trocou as ordens dos
/// fatos (0↔1), o caso que colidiria no índice único sem a estratégia limpar + flush + inserir.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class EdicaoColetaSobRetificacaoEndpointTests
{
    private const string ProcessoMediaType = "application/vnd.uniplus.processo-seletivo.v1+json";

    private static readonly object[] FatosOriginais =
    [
        new { fatoCodigo = "COR_RACA", ordem = 0, precondicao = (object?)null },
        new { fatoCodigo = "BAIXA_RENDA", ordem = 1, precondicao = (object?)null },
    ];

    // Mesmas duas coletas, ORDENS TROCADAS — o que a sessão edita e o descarte tem de desfazer.
    private static readonly object[] FatosTrocados =
    [
        new { fatoCodigo = "BAIXA_RENDA", ordem = 0, precondicao = (object?)null },
        new { fatoCodigo = "COR_RACA", ordem = 1, precondicao = (object?)null },
    ];

    private static readonly object[] RegrasOriginais =
    [
        new
        {
            codigoFato = "MODALIDADE",
            regras = new object[] { new { ordem = 0, contribui = "AC", quando = (object?)null } },
        },
    ];

    private readonly CascadingFixture _fixture;

    public EdicaoColetaSobRetificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Sob sessão, PUT /fatos-coletados sem If-Match é 428; com If-Match defasado é 412; com o corrente é 204 com ETag novo")]
    public async Task Fatos_SobSessao_Precondicao()
    {
        Contexto ctx = await PublicarComColetaAsync(nameof(Fatos_SobSessao_Precondicao));
        string etagInicial = await ctx.AbrirSessaoAsync();

        (await ctx.PutFatosAsync(FatosTrocados, ifMatch: null)).StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        HttpResponseMessage aceito = await ctx.PutFatosAsync(FatosTrocados, ifMatch: etagInicial);
        aceito.StatusCode.Should().Be(HttpStatusCode.NoContent);
        string etagNovo = LerETag(aceito);
        etagNovo.Should().NotBe(etagInicial, "toda mutação aceita sob sessão incrementa a revisão");

        (await ctx.PutFatosAsync(FatosTrocados, ifMatch: etagInicial)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact(DisplayName = "Editar fatos sob sessão TROCANDO as ordens (0↔1) de uma coleta já persistida persiste sem colisão de índice único")]
    public async Task Fatos_SobSessao_TrocaOrdens_Persiste()
    {
        // A edição sob sessão substitui a coleta inteira: as duas linhas persistidas saem e duas
        // novas entram, reusando as mesmas chaves (processo, ordem) e (processo, fatoCodigo). O EF
        // Core ordena os DELETEs antes dos INSERTs para o conflito de índice único (entidades novas,
        // PKs distintas), então a troca persiste — não há colisão nem necessidade de flush aqui,
        // diferente do descarte, cujo flush existe pela FIDELIDADE (repor as congeladas, não as vivas).
        Contexto ctx = await PublicarComColetaAsync(nameof(Fatos_SobSessao_TrocaOrdens_Persiste));
        string etag = await ctx.AbrirSessaoAsync();

        (await ctx.PutFatosAsync(FatosTrocados, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument doc = await ctx.ObterProcessoAsync();
        JsonElement fatos = doc.RootElement.GetProperty("fatosColetados");
        fatos.EnumerateArray().Select(f => f.GetProperty("fatoCodigo").GetString())
            .Should().Equal(["BAIXA_RENDA", "COR_RACA"], "a troca de ordens sob sessão persistiu");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        db.Set<FatoColetado>().Count(f => f.ProcessoSeletivoId == ctx.ProcessoId).Should().Be(2, "sem linha órfã");
    }

    [Fact(DisplayName = "Editar fatos de um processo publicado SEM sessão é 422")]
    public async Task Fatos_PublicadoSemSessao_422()
    {
        Contexto ctx = await PublicarComColetaAsync(nameof(Fatos_PublicadoSemSessao_422));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(FatosTrocados, ifMatch: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "O descarte da sessão que TROCOU as ordens dos fatos repõe a coleta congelada, sem colisão de índice único e sem órfãos")]
    public async Task Descarte_ComOrdensTrocadas_ReporFielmente()
    {
        Contexto ctx = await PublicarComColetaAsync(nameof(Descarte_ComOrdensTrocadas_ReporFielmente));
        string etag = await ctx.AbrirSessaoAsync();

        // A sessão troca as ordens (0↔1) — sem o limpar+flush+inserir, o descarte colidiria no
        // índice único (processo, ordem) ao repor as congeladas por cima das editadas.
        string etagAposFatos = LerETag(await Aceito(ctx.PutFatosAsync(FatosTrocados, ifMatch: etag)));

        // Descarta com o ETag corrente — é aqui que a restauração fiel roda.
        HttpResponseMessage descarte = await ctx.DescartarAsync(ifMatch: etagAposFatos);
        descarte.StatusCode.Should().Be(HttpStatusCode.NoContent, "o descarte não pode colidir no índice único ao repor as congeladas");

        // A coleta voltou byte-a-byte à ordem congelada.
        using JsonDocument doc = await ctx.ObterProcessoAsync();
        JsonElement fatos = doc.RootElement.GetProperty("fatosColetados");
        fatos.EnumerateArray().Select(f => f.GetProperty("fatoCodigo").GetString())
            .Should().Equal(["COR_RACA", "BAIXA_RENDA"], "as ordens congeladas voltaram");
        fatos[0].GetProperty("ordem").GetInt32().Should().Be(0);
        fatos[1].GetProperty("ordem").GetInt32().Should().Be(1);

        // Sem órfãos: exatamente dois fatos coletados persistidos.
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        int total = db.Set<FatoColetado>().Count(f => f.ProcessoSeletivoId == ctx.ProcessoId);
        total.Should().Be(2, "o descarte não deixa linha órfã");
    }

    private static async Task<HttpResponseMessage> Aceito(Task<HttpResponseMessage> chamada)
    {
        HttpResponseMessage r = await chamada;
        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return r;
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid DocumentoId)
    {
        public Task<HttpResponseMessage> PutFatosAsync(IReadOnlyList<object> corpo, string? ifMatch) =>
            EnviarAsync(HttpMethod.Put, "fatos-coletados", corpo, ifMatch);

        public Task<HttpResponseMessage> PutRegrasAsync(IReadOnlyList<object> corpo, string? ifMatch) =>
            EnviarAsync(HttpMethod.Put, "regras-derivacao", corpo, ifMatch);

        public async Task<string> AbrirSessaoAsync()
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/retificacao-em-curso", UriKind.Relative))
            {
                Content = JsonContent.Create(new { motivo = "Correção da coleta" }),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.Created);
            return LerETag(resposta);
        }

        public async Task<HttpResponseMessage> DescartarAsync(string ifMatch)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Delete,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/retificacao-em-curso", UriKind.Relative));
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
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

        private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string recurso, object corpo, string? ifMatch)
        {
            using HttpRequestMessage request = new(
                metodo,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/{recurso}", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

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
            Autenticar(publicar);
            publicar.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            HttpResponseMessage resposta = await Client.SendAsync(publicar).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, "o cenário depende de um certame publicado com coleta");
        }
    }

    private async Task<Contexto> PublicarComColetaAsync(string nome)
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

        Contexto ctx = new(api, client, processoId, documentoId);

        // Coleta e derivação definidas em rascunho, ANTES de publicar — para o congelamento e a
        // restauração terem o que repor.
        (await ctx.PutFatosAsync(FatosOriginais, ifMatch: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.PutRegrasAsync(RegrasOriginais, ifMatch: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ctx.PublicarAsync();

        return ctx;
    }

    private static string LerETag(HttpResponseMessage resposta)
    {
        resposta.Headers.ETag.Should().NotBeNull(
            $"a resposta {(int)resposta.StatusCode} de uma sessão editorial carrega o ETag");
        return resposta.Headers.ETag!.ToString();
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string HojeMais(int dias) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
