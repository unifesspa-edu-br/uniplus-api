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

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Integration tests do admin CRUD de <c>ObrigatoriedadeLegal</c>
/// contra Postgres real via Testcontainers. Cobrem:
/// happy path POST/PUT/DELETE restrito à permissão de manutenção,
/// validação 422, 404 quando inexistente, idempotência POST e
/// conflito de RegraCodigo duplicado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ObrigatoriedadeLegalAdminEndpointTests : ObrigatoriedadeLegalAdminTestBase
{
    private static readonly Dictionary<string, object> PredicadoEtapaProvaObjetiva = new(StringComparer.Ordinal)
    {
        ["$tipo"] = "etapaObrigatoria",
        // Código de um tipo de etapa ativo e semeado (CriaCadastroTiposEtapa) —
        // este teste passa pelo endpoint real, então o handler valida contra o cadastro.
        ["tipoEtapaCodigo"] = "PROVA_OBJETIVA",
    };

    public ObrigatoriedadeLegalAdminEndpointTests(CascadingFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "POST admin/obrigatoriedades-legais com a permissão de manutenção cria regra universal")]
    public async Task Criar_PlataformaAdmin_CriaRegraUniversal()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraCodigo = UniqueRegraCodigo();

        object payload = CorposDeObrigatoriedadeLegal.RegraUniversal(regraCodigo, PredicadoEtapaProvaObjetiva);

        HttpResponseMessage response = await PostAsync(client, "/api/selecao/admin/obrigatoriedades-legais", payload);

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"esperava Created mas veio {response.StatusCode}. Body: {body}");
        await VerificarRegraGravadaAsync(regraCodigo);
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400 ou 422")]
    public async Task Criar_SemIdempotencyKey_Retorna4xx()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        object payload = CorposDeObrigatoriedadeLegal.RegraSemChave(UniqueRegraCodigo(), PredicadoConcorrenciaDupla);

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri("/api/selecao/admin/obrigatoriedades-legais", UriKind.Relative));
        request.Content = JsonContent.Create(payload);
        // Sem Idempotency-Key header.

        HttpResponseMessage response = await client.SendAsync(request);

        ((int)response.StatusCode).Should().BeOneOf([400, 422],
            "RequiresIdempotencyKey filter rejeita request sem header");
    }

    [Fact(DisplayName = "POST com tipo de processo inexistente ou inativo retorna 422 específico")]
    public async Task Criar_TipoProcessoInexistenteOuInativo_Retorna422()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        object payload = CorposDeObrigatoriedadeLegal.RegraComTipoInvalido(UniqueRegraCodigo(), PredicadoConcorrenciaDupla);

        HttpResponseMessage response = await PostAsync(client, "/api/selecao/admin/obrigatoriedades-legais", payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith("uniplus.selecao.obrigatoriedade_legal.tipo_processo_nao_encontrado_ou_inativo");
    }

    [Fact(DisplayName = "POST com Idempotency-Key repetido retorna 200 com mesmo Id (idempotência)")]
    public async Task Criar_IdempotencyKeyRepetido_RetornaMesmaResposta()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraCodigo = UniqueRegraCodigo();
        string idempotencyKey = Guid.NewGuid().ToString();

        object payload = CorposDeObrigatoriedadeLegal.RegraIdempotente(regraCodigo, PredicadoConcorrenciaDupla);

        Guid primeiroId = await CriarRegraIdempotenteAsync(client, payload, idempotencyKey);

        HttpResponseMessage repetida = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", payload, idempotencyKey);

        await VerificarRespostaIdempotenteAsync(repetida, primeiroId);
    }

    [Fact(DisplayName = "POST com RegraCodigo duplicado retorna 409 com type específico")]
    public async Task Criar_RegraCodigoDuplicado_Retorna409()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraCodigo = UniqueRegraCodigo();

        await CriarRegraOriginalAsync(client, regraCodigo);

        // Tenta criar OUTRA regra com mesmo RegraCodigo mas conteúdo diferente
        // (baseLegal mudada → hash distinto, então NÃO bate na constraint de Hash;
        // o UNIQUE parcial sobre regra_codigo é o que defende).
        object duplicado = CorposDeObrigatoriedadeLegal.RegraDuplicada(regraCodigo, PredicadoConcorrenciaDupla);

        HttpResponseMessage segunda = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", duplicado);

        await VerificarConflitoDeRegraDuplicadaAsync(segunda);
    }

    [Fact(DisplayName = "PUT full-replace altera campo e mantém URI estável")]
    public async Task Atualizar_FullReplace_MantemUriEstavel()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraCodigo = UniqueRegraCodigo();

        Guid id = await SeedRegraAsync(client, regraCodigo, baseLegal: "Lei original");

        object putPayload = CorposDeObrigatoriedadeLegal.RegraAtualizada(id, regraCodigo, PredicadoConcorrenciaDupla);

        await AtualizarRegraAsync(client, id, putPayload);

        await VerificarAtualizacaoGravadaAsync(id);
    }

    [Fact(DisplayName = "PUT com Id path divergente do body retorna 400")]
    public async Task Atualizar_IdPathDivergenteBody_Retorna400()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraCodigo = UniqueRegraCodigo();
        Guid idReal = await SeedRegraAsync(client, regraCodigo);
        Guid idDivergente = Guid.NewGuid();

        object putPayload = CorposDeObrigatoriedadeLegal.RegraComIdDivergente(idDivergente, regraCodigo, PredicadoConcorrenciaDupla);

        HttpResponseMessage response = await PutAsync(
            client,
            $"/api/selecao/admin/obrigatoriedades-legais/{idReal}",
            putPayload);

        await RecusaDoIdDivergente.ConferirAsync(response);
    }

    [Fact(DisplayName = "DELETE soft-delete retorna 204 e regra some do GET público")]
    public async Task Desativar_SoftDelete_ScrubsPublicGet()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraCodigo = UniqueRegraCodigo();
        Guid id = await SeedRegraAsync(client, regraCodigo);

        HttpResponseMessage delete = await client.DeleteAsync(
            new Uri($"/api/selecao/admin/obrigatoriedades-legais/{id}", UriKind.Relative));
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await VerificarRegraDesativadaNoCatalogoAsync(client, id);

        await VerificarHistoricoDaDesativacaoAsync(id);
    }

    [Fact(DisplayName = "GET com filtros admin retorna apenas regras matching")]
    public async Task Listar_ComFiltros_RetornaSubconjunto()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraCodigoEtapa = UniqueRegraCodigo();
        string regraCodigoModalidade = UniqueRegraCodigo();

        await SeedRegraAsync(client, regraCodigoEtapa, categoria: "ETAPA");
        await SeedRegraAsync(client, regraCodigoModalidade, categoria: "MODALIDADE");

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/selecao/obrigatoriedades-legais?categoria=ETAPA&limit=50", UriKind.Relative));

        await VerificarFiltroDeCategoriaAsync(response, regraCodigoEtapa, regraCodigoModalidade);
    }

    [Fact(DisplayName = "GET filtra por tipoProcesso e ignora o parâmetro legado")]
    public async Task Listar_TipoProcessoFiltraEParametroLegadoEIgnorado()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string regraSiSU = UniqueRegraCodigo();
        string regraPSIQ = UniqueRegraCodigo();
        await SeedRegraAsync(client, regraSiSU, tipoProcessoCodigo: "SISU");
        await SeedRegraAsync(client, regraPSIQ, tipoProcessoCodigo: "INDIGENA_E_QUILOMBOLA");

        await VerificarFiltroDeTipoProcessoAsync(client, regraSiSU, regraPSIQ);

        await VerificarParametroLegadoAsync(client, regraSiSU, regraPSIQ);
    }

    private static async Task VerificarRespostaIdempotenteAsync(HttpResponseMessage repetida, Guid primeiroId)
    {
        ((int)repetida.StatusCode).Should().BeOneOf([200, 201],
            "replay da mesma Idempotency-Key retorna a representação anterior");

        string repetidoBody = await repetida.Content.ReadAsStringAsync();
        Guid repetidoId = JsonSerializer.Deserialize<Guid>(repetidoBody);
        repetidoId.Should().Be(primeiroId, "replay devolve o mesmo Id");
    }

    private static async Task CriarRegraOriginalAsync(HttpClient client, string regraCodigo)
    {
        object payload = CorposDeObrigatoriedadeLegal.RegraOriginal(regraCodigo, PredicadoConcorrenciaDupla);
        HttpResponseMessage primeira = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", payload);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task VerificarConflitoDeRegraDuplicadaAsync(HttpResponseMessage segunda)
    {
        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith("uniplus.selecao.obrigatoriedade_legal.regra_codigo_duplicada");
    }

    private static async Task AtualizarRegraAsync(HttpClient client, Guid id, object putPayload)
    {
        HttpResponseMessage put = await PutAsync(
            client,
            $"/api/selecao/admin/obrigatoriedades-legais/{id}",
            putPayload);

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task VerificarRegraDesativadaNoCatalogoAsync(HttpClient client, Guid id)
    {
        // GET público — query filter aplica IsDeleted=false, então 404.
        HttpResponseMessage publicGet = await client.GetAsync(
            new Uri($"/api/selecao/obrigatoriedades-legais/{id}", UriKind.Relative));
        publicGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task VerificarFiltroDeCategoriaAsync(HttpResponseMessage response, string regraCodigoEtapa, string regraCodigoModalidade)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        IReadOnlyList<string> regraCodigos = [.. doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("regraCodigo").GetString()!)];

        regraCodigos.Should().Contain(regraCodigoEtapa);
        regraCodigos.Should().NotContain(regraCodigoModalidade, "filtro categoria=ETAPA exclui modalidade");
    }

    private static async Task VerificarFiltroDeTipoProcessoAsync(HttpClient client, string regraSiSU, string regraPSIQ)
    {
        HttpResponseMessage filtrada = await client.GetAsync(
            new Uri("/api/selecao/obrigatoriedades-legais?tipoProcesso=SISU&limit=50", UriKind.Relative));
        using JsonDocument filtradaJson = JsonDocument.Parse(await filtrada.Content.ReadAsStringAsync());
        IReadOnlyList<string> filtradas = [.. filtradaJson.RootElement.EnumerateArray()
            .Select(static item => item.GetProperty("regraCodigo").GetString()!)];
        filtradas.Should().Contain(regraSiSU);
        filtradas.Should().NotContain(regraPSIQ);
    }

    private async Task VerificarRegraGravadaAsync(string regraCodigo)
    {
        await using (AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope())
        await using (UniPlusDbContext db = ResolveDbContext(scope))
        {
            ObrigatoriedadeLegal? regra = await db.ObrigatoriedadesLegais
                .FirstOrDefaultAsync(o => o.RegraCodigo == regraCodigo);
            regra.Should().NotBeNull();
        }
    }

    private async Task VerificarAtualizacaoGravadaAsync(Guid id)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await using UniPlusDbContext db = ResolveDbContext(scope);
        ObrigatoriedadeLegal regra = await db.ObrigatoriedadesLegais.SingleAsync(o => o.Id == id);
        regra.BaseLegal.Should().Be("Lei 14.723/2023 art.2º — nova");

        int historico = await db.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == id)
            .CountAsync();
        historico.Should().Be(2, "insert + update geram 2 linhas no histórico forensic");
    }

    private async Task VerificarHistoricoDaDesativacaoAsync(Guid id)
    {
        // Histórico forensic mantém a regra com snapshot pós-soft-delete.
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await using UniPlusDbContext db = ResolveDbContext(scope);
        int historico = await db.ObrigatoriedadeLegalHistorico
            .Where(h => h.RegraId == id)
            .CountAsync();
        historico.Should().BeGreaterThanOrEqualTo(2, "insert + soft-delete = 2 snapshots no mínimo");
    }

    private static async Task VerificarParametroLegadoAsync(HttpClient client, string regraSiSU, string regraPSIQ)
    {
        HttpResponseMessage legado = await client.GetAsync(
            new Uri("/api/selecao/obrigatoriedades-legais?tipoEdital=SiSU&limit=50", UriKind.Relative));
        using JsonDocument legadoJson = JsonDocument.Parse(await legado.Content.ReadAsStringAsync());
        IReadOnlyList<string> semFiltro = [.. legadoJson.RootElement.EnumerateArray()
            .Select(static item => item.GetProperty("regraCodigo").GetString()!)];
        semFiltro.Should().Contain(regraSiSU);
        semFiltro.Should().Contain(regraPSIQ);
    }

    private static async Task<Guid> CriarRegraIdempotenteAsync(HttpClient client, object payload, string idempotencyKey)
    {
        HttpResponseMessage primeira = await PostAsync(
            client, "/api/selecao/admin/obrigatoriedades-legais", payload, idempotencyKey);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        string primeiroBody = await primeira.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Guid>(primeiroBody);
    }

    private static async Task<Guid> SeedRegraAsync(
        HttpClient client,
        string regraCodigo,
        string categoria = "OUTROS",
        string baseLegal = "Lei seed",
        string tipoProcessoCodigo = "*")
    {
        object payload = CorposDeObrigatoriedadeLegal.RegraInicial(regraCodigo, categoria, baseLegal, tipoProcessoCodigo, PredicadoConcorrenciaDupla);

        HttpResponseMessage response = await PostAsync(client, "/api/selecao/admin/obrigatoriedades-legais", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "seed precisa criar a regra para o teste prosseguir; body: "
            + await response.Content.ReadAsStringAsync());
        string body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Guid>(body);
    }
}
