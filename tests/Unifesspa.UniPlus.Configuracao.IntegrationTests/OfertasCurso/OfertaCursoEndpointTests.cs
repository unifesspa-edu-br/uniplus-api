namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>OfertaCurso</c> (story #588,
/// issue #749): routing, vendor media type, HATEOAS, autenticação/autorização,
/// idempotência, referências vivas (curso/local/unidade), <b>congelamento do
/// snapshot da unidade ofertante com Unidade REAL semeada no schema
/// organizacao</b> (ADR-0061, padrão LeituraInProcessTests — o monólito
/// co-hospeda os módulos e o IUnidadeReader resolve in-process), guard da base
/// legal condicional (422), ciclo CRUD completo e CA-05 (curso referenciado por
/// oferta viva bloqueia a remoção do curso; remover a oferta libera).
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class OfertaCursoEndpointTests
{
    private static readonly DateOnly VigenciaInicio = new(2026, 1, 1);
    private static readonly DateTimeOffset Agora = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoEndpointFixture _fixture;

    public OfertaCursoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/ofertas-curso", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.oferta-curso.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/ofertas-curso sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/ofertas-curso", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/ofertas-curso", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { });

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a policy [Authorize(Roles = \"plataforma-admin\")] nega um principal autenticado sem o role");
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/ofertas-curso", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria (201) congelando o snapshot da Unidade REAL semeada; o GET expõe o sub-objeto + HATEOAS")]
    public async Task Criar_CongelaUnidadeRealSemeada()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        Unidade unidade = await SemearUnidadeAsync("faceel-oferta", "FACEEL", "OFC001");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            programaDeOferta = "REGULAR",
            formatoPedagogico = "PRESENCIAL",
            turno = "MATUTINO",
            eMecCodigo = "123456",
            codigoSga = "ENG-01",
            vagasAnuaisAutorizadas = 40,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("cursoId").GetGuid().Should().Be(cursoId);
        root.GetProperty("localOfertaId").GetGuid().Should().Be(localId);
        root.GetProperty("programaDeOferta").GetString().Should().Be("REGULAR");
        root.GetProperty("formatoPedagogico").GetString().Should().Be("PRESENCIAL");
        root.GetProperty("turno").GetString().Should().Be("MATUTINO");
        root.GetProperty("eMecCodigo").GetString().Should().Be("123456");
        root.GetProperty("codigoSga").GetString().Should().Be("ENG-01");
        root.GetProperty("vagasAnuaisAutorizadas").GetInt32().Should().Be(40);

        // Congelamento (ADR-0061): o sub-objeto reflete a Unidade viva resolvida
        // in-process pelo IUnidadeReader no ato da criação.
        JsonElement unidadeOfertante = root.GetProperty("unidadeOfertante");
        unidadeOfertante.GetProperty("origemId").GetGuid().Should().Be(unidade.Id);
        unidadeOfertante.GetProperty("sigla").GetString().Should().Be("FACEEL");
        unidadeOfertante.GetProperty("nome").GetString().Should().Be("Unidade FACEEL");
        unidadeOfertante.GetProperty("tipo").GetString().Should().Be(nameof(TipoUnidade.Centro));

        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST com unidade ofertante inexistente retorna 422 — não há identidade viva para congelar")]
    public async Task Criar_UnidadeInexistente_Retorna422()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = Guid.NewGuid(),
            programaDeOferta = "REGULAR",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com curso inexistente retorna 422")]
    public async Task Criar_CursoInexistente_Retorna422()
    {
        (_, Guid localId) = await SemearCursoELocalAsync();
        Unidade unidade = await SemearUnidadeAsync("ilinguas-oferta", "ILINGUAS", "OFC002");

        var body = new
        {
            cursoId = Guid.NewGuid(),
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            programaDeOferta = "REGULAR",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com programa não-REGULAR sem base legal retorna 422 (guard de domínio)")]
    public async Task Criar_ProgramaNaoRegularSemBaseLegal_Retorna422()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        Unidade unidade = await SemearUnidadeAsync("iedar-oferta", "IEDAR", "OFC003");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            programaDeOferta = "PARFOR",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Ciclo CRUD completo: POST → PUT (204, guard revalidado) → GET reflete → DELETE (204) → GET 404")]
    public async Task CicloCrudCompleto_CriaEditaRemove()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        Unidade unidade = await SemearUnidadeAsync("isaude-oferta", "ISAUDE", "OFC004");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            programaDeOferta = "REGULAR",
            turno = "MATUTINO",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        // PUT Regular→Parfor sem base legal: guard revalidado na transição → 422.
        var putSemBase = new { id, programaDeOferta = "PARFOR" };
        HttpResponseMessage transicaoInvalida = await EnviarPutAdmin(client, id, putSemBase);
        transicaoInvalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a transição Regular→Parfor sem base legal viola o guard condicional (ADR-0066)");

        // PUT válido: Parfor com base legal, formato EAD, sem turno.
        var putValido = new
        {
            id,
            programaDeOferta = "PARFOR",
            formatoPedagogico = "EAD",
            eMecCodigo = "654321",
            vagasAnuaisAutorizadas = 0,
            baseLegal = "Decreto 6.755/2009",
        };
        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, putValido);
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync()))
        {
            JsonElement root = doc.RootElement;
            root.GetProperty("programaDeOferta").GetString().Should().Be("PARFOR");
            root.GetProperty("formatoPedagogico").GetString().Should().Be("EAD");
            root.GetProperty("turno").ValueKind.Should().Be(JsonValueKind.Null, "o PUT não enviou turno");
            root.GetProperty("eMecCodigo").GetString().Should().Be("654321");
            root.GetProperty("vagasAnuaisAutorizadas").GetInt32().Should().Be(0, "zero é teto válido");
            root.GetProperty("baseLegal").GetString().Should().Be("Decreto 6.755/2009");
            root.GetProperty("cursoId").GetGuid().Should().Be(cursoId, "curso é imutável");
            root.GetProperty("unidadeOfertante").GetProperty("sigla").GetString()
                .Should().Be("ISAUDE", "o snapshot congelado é imutável pós-criação");
        }

        // DELETE: soft-delete simples — sem 409 (snapshots externos são desacoplados).
        HttpResponseMessage remover = await EnviarDeleteAdmin(client, id);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage aposRemocao = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso/{id}", UriKind.Relative));
        aposRemocao.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "o soft-delete tira a oferta das leituras via query filter global");
    }

    [Fact(DisplayName = "CA-05: curso com oferta viva bloqueia DELETE do curso (409); remover a oferta libera (204)")]
    public async Task Ca05_OfertaVivaBloqueiaRemocaoDoCurso()
    {
        (Guid cursoId, Guid localId) = await SemearCursoELocalAsync();
        Unidade unidade = await SemearUnidadeAsync("igeo-oferta", "IGEOF", "OFC005");

        var body = new
        {
            cursoId,
            localOfertaId = localId,
            unidadeOfertanteOrigemId = unidade.Id,
            programaDeOferta = "REGULAR",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid ofertaId = await criar.Content.ReadFromJsonAsync<Guid>();

        // Curso referenciado por oferta viva → remoção bloqueada (409).
        using HttpRequestMessage removerCursoBloqueado = NovoDeleteAdmin(
            $"/api/configuracao/admin/cursos/{cursoId}");
        HttpResponseMessage bloqueado = await client.SendAsync(removerCursoBloqueado);
        bloqueado.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "ReferenciadoPorOfertaCursoVivaAsync agora consulta oferta_curso de verdade (CA-05)");

        // Remover a oferta (204) libera o curso.
        HttpResponseMessage removerOferta = await EnviarDeleteAdmin(client, ofertaId);
        removerOferta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpRequestMessage removerCursoLiberado = NovoDeleteAdmin(
            $"/api/configuracao/admin/cursos/{cursoId}");
        HttpResponseMessage liberado = await client.SendAsync(removerCursoLiberado);
        liberado.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "o soft-delete da oferta libera o curso para remoção");
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso?cursoId={id} retorna só as ofertas do curso; curso sem oferta → 200 vazio")]
    public async Task Listar_FiltraPorCursoId_RetornaSomenteDoCurso()
    {
        (Guid cursoA, Guid localId) = await SemearCursoELocalAsync();
        Guid cursoB = await SemearCursoAsync("Arquitetura e Urbanismo");
        Unidade unidade = await SemearUnidadeAsync("facomp-755-a", "FCP755A", "OFC755A");

        using HttpClient client = _fixture.Factory.CreateClient();
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarPostAdmin(client, CorpoOferta(cursoB, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Filtro por A (limit folgado): exatamente as 2 ofertas de A, nenhuma de B.
        HttpResponseMessage respA = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={cursoA}&limit=50", UriKind.Relative));
        respA.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await respA.Content.ReadAsStringAsync()))
        {
            JsonElement root = doc.RootElement;
            root.ValueKind.Should().Be(JsonValueKind.Array, "a listagem devolve array JSON puro (ADR-0025)");
            root.GetArrayLength().Should().Be(2, "o curso A tem 2 ofertas vivas");
            foreach (JsonElement item in root.EnumerateArray())
            {
                item.GetProperty("cursoId").GetGuid().Should().Be(cursoA);
            }
        }

        // Curso sem ofertas → 200 com coleção vazia (sem 404).
        HttpResponseMessage respVazio = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={Guid.NewGuid()}", UriKind.Relative));
        respVazio.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await respVazio.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetArrayLength().Should().Be(0);
        }
    }

    [Fact(DisplayName = "GET /api/configuracao/ofertas-curso preserva cursoId no header Link (self e next)")]
    public async Task Listar_ComFiltroCursoId_PreservaQueryParamNoLink()
    {
        (Guid cursoA, Guid localId) = await SemearCursoELocalAsync();
        Unidade unidade = await SemearUnidadeAsync("facomp-755-b", "FCP755B", "OFC755B");

        using HttpClient client = _fixture.Factory.CreateClient();
        // Duas ofertas do mesmo curso → com limit=1 há próxima página (rel=next).
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarPostAdmin(client, CorpoOferta(cursoA, localId, unidade.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/ofertas-curso?cursoId={cursoA}&limit=1", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Link", out IEnumerable<string>? links).Should().BeTrue();
        string link = links!.Single();
        link.Should().Contain("rel=\"next\"", "limit=1 com 2 ofertas filtradas deve emitir próxima página");
        link.Should().Contain($"cursoId={cursoA}", "o filtro de curso deve viajar em self e next para o cliente reanexar");
    }

    // ── Seeds (padrão LeituraInProcessTests: resolve DbContexts do container do host) ──

    private static object CorpoOferta(Guid cursoId, Guid localOfertaId, Guid unidadeOfertanteOrigemId) => new
    {
        cursoId,
        localOfertaId,
        unidadeOfertanteOrigemId,
        programaDeOferta = "REGULAR",
        formatoPedagogico = "PRESENCIAL",
    };

    private async Task<Guid> SemearCursoAsync(string nome)
    {
        Curso curso = Curso.Criar(CodigoUnico(), nome, "Bacharelado", "Graduação", null).Value!;

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        dbContext.Cursos.Add(curso);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return curso.Id;
    }

    private async Task<(Guid CursoId, Guid LocalOfertaId)> SemearCursoELocalAsync()
    {
        Curso curso = Curso.Criar(
            CodigoUnico(), "Engenharia Civil", "Bacharelado", "Graduação", null).Value!;
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        dbContext.Cursos.Add(curso);
        dbContext.LocaisOferta.Add(local);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return (curso.Id, local.Id);
    }

    private async Task<Unidade> SemearUnidadeAsync(string slug, string sigla, string codigo)
    {
        Unidade unidade = Unidade.Criar(
            nome: $"Unidade {sigla}",
            alias: null,
            slug: Slug.From(slug).Value!,
            sigla: sigla,
            codigo: codigo,
            unidadeSuperiorId: null,
            tipo: TipoUnidade.Centro,
            unidadeAcademica: true,
            vigenciaInicio: VigenciaInicio,
            vigenciaFim: null,
            origem: OrigemUnidade.CriadoNoUniPlus).Value!;

        // Semeia pelo DbContext do MÓDULO Organização resolvido do container do
        // host — o schema `organizacao` foi criado pelas migrations no boot; o
        // IUnidadeReader (in-process, ADR-0056) enxerga a Unidade na criação da oferta.
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();

        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return unidade;
    }

    // ── Helpers HTTP ──────────────────────────────────────────────────────

    private static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/ofertas-curso", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/ofertas-curso/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = NovoDeleteAdmin($"/api/configuracao/admin/ofertas-curso/{id}");
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage NovoDeleteAdmin(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }
}
