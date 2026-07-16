namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Integration tests do admin CRUD de <c>ObrigatoriedadeLegal</c>
/// (Story #461) contra Postgres real via Testcontainers. Cobrem:
/// happy path POST/PUT/DELETE restrito ao role <c>plataforma-admin</c>,
/// validação 422, 404 quando inexistente, idempotência POST e
/// conflito de RegraCodigo duplicado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ObrigatoriedadeLegalAdminEndpointTests : IAsyncLifetime
{
    private const string AdminPlataforma = "plataforma-admin";

    // Predicados em Dictionary porque anonymous types não suportam keys que
    // começam com '$' (o JsonPolymorphic do PredicadoObrigatoriedade usa
    // "$tipo" como discriminator per ADR-0058).
    private static readonly Dictionary<string, object> PredicadoConcorrenciaDupla = new(StringComparer.Ordinal)
    {
        ["$tipo"] = "concorrenciaDuplaObrigatoria",
    };

    private static readonly Dictionary<string, object> PredicadoEtapaProvaObjetiva = new(StringComparer.Ordinal)
    {
        ["$tipo"] = "etapaObrigatoria",
        ["tipoEtapaCodigo"] = "ProvaObjetiva",
    };

    private readonly CascadingFixture _fixture;

    public ObrigatoriedadeLegalAdminEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// A <see cref="CascadingFixture"/> compartilha UM Postgres entre TODAS as classes da
    /// coleção (Story #853 tornou isso perigoso): uma <c>ObrigatoriedadeLegal</c> universal
    /// (<c>"*"</c>) que esta classe cria com vigência aberta sobreviveria para o resto da
    /// suíte, e o gate legal novo passaria a avaliar TODO <c>Publicar</c>/<c>Retificar</c>/
    /// <c>FecharRetificacao</c> de OUTRAS classes contra ela — reprovando processos que nunca
    /// tiveram nada a ver com esta suíte. Soft-delete de tudo que esta classe criou
    /// (identificado pelo prefixo <c>TEST_</c> de <see cref="UniqueRegraCodigo"/>) devolve o
    /// catálogo ao estado anterior para a próxima classe da coleção.
    /// </summary>
    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await using SelecaoDbContext db = ResolveDbContext(scope);

        await db.Database.ExecuteSqlAsync(
            $"UPDATE selecao.obrigatoriedades_legais SET is_deleted = true WHERE regra_codigo LIKE 'TEST\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "POST admin/obrigatoriedades-legais como plataforma-admin cria regra universal")]
    public async Task Criar_PlataformaAdmin_CriaRegraUniversal()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraCodigo = UniqueRegraCodigo();

        object payload = new
        {
            tipoProcessoCodigo = "*",
            categoria = "etapa",
            regraCodigo,
            predicado = PredicadoEtapaProvaObjetiva,
            descricaoHumana = "Edital deve incluir Prova Objetiva",
            baseLegal = "Lei 12.711/2012 art.1º",
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
            atoNormativoUrl = (string?)null,
            portariaInternaCodigo = (string?)null,
        };

        HttpResponseMessage response = await PostAsync(client, "/api/selecao/admin/obrigatoriedades-legais", payload);

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"esperava Created mas veio {response.StatusCode}. Body: {body}");
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        await using (SelecaoDbContext db = ResolveDbContext(scope))
        {
            ObrigatoriedadeLegal? regra = await db.ObrigatoriedadesLegais
                .FirstOrDefaultAsync(o => o.RegraCodigo == regraCodigo);
            regra.Should().NotBeNull();
        }
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400 ou 422")]
    public async Task Criar_SemIdempotencyKey_Retorna4xx()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);

        object payload = new
        {
            tipoProcessoCodigo = "*",
            categoria = "outros",
            regraCodigo = UniqueRegraCodigo(),
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "x",
            baseLegal = "Lei",
            vigenciaInicio = "2026-01-01",
        };

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri("/api/selecao/admin/obrigatoriedades-legais", UriKind.Relative));
        request.Content = JsonContent.Create(payload);
        // Sem Idempotency-Key header.

        HttpResponseMessage response = await client.SendAsync(request);

        ((int)response.StatusCode).Should().BeOneOf([400, 422],
            "RequiresIdempotencyKey filter rejeita request sem header");
    }

    [Fact(DisplayName = "POST com tipo de processo fora do vocabulário retorna 422 específico")]
    public async Task Criar_TipoProcessoForaDoVocabulario_Retorna422()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);

        object payload = new
        {
            tipoProcessoCodigo = "SISU_ANTIGO",
            categoria = "outros",
            regraCodigo = UniqueRegraCodigo(),
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "Tipo inválido",
            baseLegal = "Lei",
            vigenciaInicio = "2026-01-01",
        };

        HttpResponseMessage response = await PostAsync(client, "/api/selecao/admin/obrigatoriedades-legais", payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith("uniplus.selecao.obrigatoriedade_legal.tipo_processo_codigo_fora_do_vocabulario");
    }

    [Fact(DisplayName = "POST com Idempotency-Key repetido retorna 200 com mesmo Id (idempotência)")]
    public async Task Criar_IdempotencyKeyRepetido_RetornaMesmaResposta()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraCodigo = UniqueRegraCodigo();
        string idempotencyKey = Guid.NewGuid().ToString();

        object payload = new
        {
            tipoProcessoCodigo = "*",
            categoria = "outros",
            regraCodigo,
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "Regra idempotente",
            baseLegal = "Lei",
            vigenciaInicio = "2026-01-01",
        };

        HttpResponseMessage primeira = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", payload, idempotencyKey);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        string primeiroBody = await primeira.Content.ReadAsStringAsync();
        Guid primeiroId = JsonSerializer.Deserialize<Guid>(primeiroBody);

        HttpResponseMessage repetida = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", payload, idempotencyKey);

        ((int)repetida.StatusCode).Should().BeOneOf([200, 201],
            "replay da mesma Idempotency-Key retorna a representação anterior");

        string repetidoBody = await repetida.Content.ReadAsStringAsync();
        Guid repetidoId = JsonSerializer.Deserialize<Guid>(repetidoBody);
        repetidoId.Should().Be(primeiroId, "replay devolve o mesmo Id");
    }

    [Fact(DisplayName = "POST com RegraCodigo duplicado retorna 409 com type específico")]
    public async Task Criar_RegraCodigoDuplicado_Retorna409()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraCodigo = UniqueRegraCodigo();

        object payload = new
        {
            tipoProcessoCodigo = "*",
            categoria = "outros",
            regraCodigo,
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "Regra original",
            baseLegal = "Lei",
            vigenciaInicio = "2026-01-01",
        };

        HttpResponseMessage primeira = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", payload);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        // Tenta criar OUTRA regra com mesmo RegraCodigo mas conteúdo diferente
        // (baseLegal mudada → hash distinto, então NÃO bate na constraint de Hash;
        // o UNIQUE parcial sobre regra_codigo é o que defende).
        object duplicado = new
        {
            tipoProcessoCodigo = "*",
            categoria = "outros",
            regraCodigo,
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "Regra duplicada com outro conteúdo",
            baseLegal = "Lei 14.723/2023",
            vigenciaInicio = "2026-02-01",
        };

        HttpResponseMessage segunda = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", duplicado);

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith("uniplus.selecao.obrigatoriedade_legal.regra_codigo_duplicada");
    }

    [Fact(DisplayName = "PUT full-replace altera campo e mantém URI estável")]
    public async Task Atualizar_FullReplace_MantemUriEstavel()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraCodigo = UniqueRegraCodigo();

        Guid id = await SeedRegraAsync(client, regraCodigo, baseLegal: "Lei original");

        object putPayload = new
        {
            id,
            tipoProcessoCodigo = "*",
            categoria = "outros",
            regraCodigo,
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "Regra atualizada",
            baseLegal = "Lei 14.723/2023 art.2º — nova",
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
            atoNormativoUrl = (string?)null,
            portariaInternaCodigo = (string?)null,
        };

        HttpResponseMessage put = await PutAsync(
            client,
            $"/api/selecao/admin/obrigatoriedades-legais/{id}",
            putPayload);

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await using SelecaoDbContext db = ResolveDbContext(scope);
        ObrigatoriedadeLegal regra = await db.ObrigatoriedadesLegais.SingleAsync(o => o.Id == id);
        regra.BaseLegal.Should().Be("Lei 14.723/2023 art.2º — nova");

        int historico = await db.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == id)
            .CountAsync();
        historico.Should().Be(2, "insert + update geram 2 linhas no histórico forensic");
    }

    [Fact(DisplayName = "PUT com Id path divergente do body retorna 400")]
    public async Task Atualizar_IdPathDivergenteBody_Retorna400()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraCodigo = UniqueRegraCodigo();
        Guid idReal = await SeedRegraAsync(client, regraCodigo);
        Guid idDivergente = Guid.NewGuid();

        object putPayload = new
        {
            id = idDivergente,
            tipoProcessoCodigo = "*",
            categoria = "outros",
            regraCodigo,
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "x",
            baseLegal = "Lei",
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
            atoNormativoUrl = (string?)null,
            portariaInternaCodigo = (string?)null,
        };

        HttpResponseMessage response = await PutAsync(
            client,
            $"/api/selecao/admin/obrigatoriedades-legais/{idReal}",
            putPayload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "DELETE soft-delete retorna 204 e regra some do GET público")]
    public async Task Desativar_SoftDelete_ScrubsPublicGet()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraCodigo = UniqueRegraCodigo();
        Guid id = await SeedRegraAsync(client, regraCodigo);

        HttpResponseMessage delete = await client.DeleteAsync(
            new Uri($"/api/selecao/admin/obrigatoriedades-legais/{id}", UriKind.Relative));
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // GET público — query filter aplica IsDeleted=false, então 404.
        HttpResponseMessage publicGet = await client.GetAsync(
            new Uri($"/api/selecao/obrigatoriedades-legais/{id}", UriKind.Relative));
        publicGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Histórico forensic mantém a regra com snapshot pós-soft-delete.
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await using SelecaoDbContext db = ResolveDbContext(scope);
        int historico = await db.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == id)
            .CountAsync();
        historico.Should().BeGreaterThanOrEqualTo(2, "insert + soft-delete = 2 snapshots no mínimo");
    }

    [Fact(DisplayName = "GET com filtros admin retorna apenas regras matching")]
    public async Task Listar_ComFiltros_RetornaSubconjunto()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraCodigoEtapa = UniqueRegraCodigo();
        string regraCodigoModalidade = UniqueRegraCodigo();

        await SeedRegraAsync(client, regraCodigoEtapa, categoria: "Etapa");
        await SeedRegraAsync(client, regraCodigoModalidade, categoria: "Modalidade");

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/selecao/obrigatoriedades-legais?categoria=Etapa&limit=50", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        IReadOnlyList<string> regraCodigos = [.. doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("regraCodigo").GetString()!)];

        regraCodigos.Should().Contain(regraCodigoEtapa);
        regraCodigos.Should().NotContain(regraCodigoModalidade, "filtro categoria=Etapa exclui Modalidade");
    }

    [Fact(DisplayName = "GET filtra por tipoProcesso e ignora o parâmetro legado")]
    public async Task Listar_TipoProcessoFiltraEParametroLegadoEIgnorado()
    {
        using HttpClient client = ClientWithRoles(AdminPlataforma);
        string regraSiSU = UniqueRegraCodigo();
        string regraPSIQ = UniqueRegraCodigo();
        await SeedRegraAsync(client, regraSiSU, tipoProcessoCodigo: "SiSU");
        await SeedRegraAsync(client, regraPSIQ, tipoProcessoCodigo: "PSIQ");

        HttpResponseMessage filtrada = await client.GetAsync(
            new Uri("/api/selecao/obrigatoriedades-legais?tipoProcesso=SiSU&limit=50", UriKind.Relative));
        using JsonDocument filtradaJson = JsonDocument.Parse(await filtrada.Content.ReadAsStringAsync());
        IReadOnlyList<string> filtradas = [.. filtradaJson.RootElement.EnumerateArray()
            .Select(static item => item.GetProperty("regraCodigo").GetString()!)];
        filtradas.Should().Contain(regraSiSU);
        filtradas.Should().NotContain(regraPSIQ);

        HttpResponseMessage legado = await client.GetAsync(
            new Uri("/api/selecao/obrigatoriedades-legais?tipoEdital=SiSU&limit=50", UriKind.Relative));
        using JsonDocument legadoJson = JsonDocument.Parse(await legado.Content.ReadAsStringAsync());
        IReadOnlyList<string> semFiltro = [.. legadoJson.RootElement.EnumerateArray()
            .Select(static item => item.GetProperty("regraCodigo").GetString()!)];
        semFiltro.Should().Contain(regraSiSU);
        semFiltro.Should().Contain(regraPSIQ);
    }

    private HttpClient ClientWithRoles(params string[] roles)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, $"admin-{Guid.NewGuid():N}"[..16]);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Admin Test");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@unifesspa.edu.br");
        return client;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object payload, string? idempotencyKey = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(url, UriKind.Relative));
        request.Content = JsonContent.Create(payload);
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string url, object payload)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri(url, UriKind.Relative));
        request.Content = JsonContent.Create(payload);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static SelecaoDbContext ResolveDbContext(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

    private static async Task<Guid> SeedRegraAsync(
        HttpClient client,
        string regraCodigo,
        string categoria = "outros",
        string baseLegal = "Lei seed",
        string tipoProcessoCodigo = "*")
    {
        object payload = new
        {
            tipoProcessoCodigo,
            categoria,
            regraCodigo,
            predicado = PredicadoConcorrenciaDupla,
            descricaoHumana = "Seed",
            baseLegal,
            vigenciaInicio = "2026-01-01",
            vigenciaFim = (string?)null,
            atoNormativoUrl = (string?)null,
            portariaInternaCodigo = (string?)null,
        };

        HttpResponseMessage response = await PostAsync(client, "/api/selecao/admin/obrigatoriedades-legais", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "seed precisa criar a regra para o teste prosseguir; body: "
            + await response.Content.ReadAsStringAsync());
        string body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Guid>(body);
    }

    private static string UniqueRegraCodigo() =>
        $"TEST_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}_{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
