namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed partial class IndicadoresDoProcessoEndpointTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/processos-seletivos";
    private const string MediaType = "application/vnd.uniplus.indicadores-do-processo.v1+json";
    private const string Pessoa = "betina.neves";
    private const string SubInscrito = "indicadores.servidor.inscrito";
    private readonly CascadingFixture _fixture;

    public IndicadoresDoProcessoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Pessoa);
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, SubInscrito);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "GET de indicadores retorna contrato ordenado, valores zero, links e no-store")]
    public async Task PublicadoSemInscricoes_RetornaContratoV1()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(PublicadoSemInscricoes_RetornaContratoV1));
        await ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        using HttpResponseMessage response = await EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);

        await ConferirRespostaCacheEStatusAsync(response);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        ConferirIdentidadeEInstante(root, processoId);
        ConferirIndicadoresSemInscricoes(root);
        ConferirLinksEPrivacidade(root, processoId);
    }

    [Fact(DisplayName = "GET de indicadores exige autenticação, negociação v1 e autorização com escopo na unidade dona")]
    public async Task AutenticacaoNegociacaoEEscopo_SeguemContrato()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(AutenticacaoNegociacaoEEscopo_SeguemContrato));
        await ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        await ConferirAutenticacaoENegociacaoAsync(processoId);
        await ConferirEscopoDaConcessaoAsync(processoId);
    }

    private async Task ConferirAutenticacaoENegociacaoAsync(Guid processoId)
    {
        string rota = $"{Rota}/{processoId}/indicadores";
        using HttpResponseMessage anonimo = await EnviarAsync(null, rota, MediaType);
        using HttpResponseMessage autorizado = await EnviarAsync(Pessoa, rota, MediaType);
        using HttpResponseMessage tipoNaoAceito = await EnviarAsync(Pessoa, rota, "application/vnd.uniplus.indicadores-do-processo.v9+json");
        ConferirStatusDeAutenticacaoEContrato(anonimo, autorizado, tipoNaoAceito);
    }

    private async Task ConferirEscopoDaConcessaoAsync(Guid processoId)
    {
        Guid outraUnidade = Guid.CreateVersion7();
        await ConcederNaUnidadeAsync("indicadores.outra.unidade", outraUnidade, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        string rota = $"{Rota}/{processoId}/indicadores";
        using HttpResponseMessage semConcessao = await EnviarAsync("indicadores.sem.concessao", rota, MediaType);
        using HttpResponseMessage papelSemConcessao = await EnviarAsync(
            "indicadores.admin", rota, MediaType, new(Papel: "plataforma-admin"));
        using HttpResponseMessage outraUnidadeResponse = await EnviarAsync("indicadores.outra.unidade", rota, MediaType);
        ConferirStatusDeEscopo(semConcessao, papelSemConcessao, outraUnidadeResponse);
    }

    [Fact(DisplayName = "Rascunho é visível com permissão de configurar e ocultado de quem só acompanha")]
    public async Task Rascunho_DiferenciaConfigurarDeAcompanhar()
    {
        Guid processoId = await SemearRascunhoAsync(nameof(Rascunho_DiferenciaConfigurarDeAcompanhar));
        await ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await ConcederAsync("indicadores.somente.acompanhar", processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);

        using HttpResponseMessage configurador = await EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage acompanha = await EnviarAsync("indicadores.somente.acompanhar", $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage inexistente = await EnviarAsync(Pessoa, $"{Rota}/{Guid.CreateVersion7()}/indicadores", MediaType);

        await ConferirRascunhoAutorizadoAsync(configurador);
        acompanha.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Inscrição submetida conta uma vez; cancelamento, rascunho e comprovantes não criam indicadores extras")]
    public async Task Contagens_ConsideramSomenteEstadosAtuaisDaInscricao()
    {
        Guid processoId = await SemearPublicadoAsync(
            nameof(Contagens_ConsideramSomenteEstadosAtuaisDaInscricao),
            new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero));
        await SemearInscricoesAsync(processoId);
        await DescartarRascunhoPelaApiAsync(processoId);
        await ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);

        using HttpResponseMessage response = await EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);

        await ConferirContagensAsync(response, 2L, 2L, 2L);
    }

    [Fact(DisplayName = "Servidor com inscrição submetida não consulta indicadores administrativos")]
    public async Task ServidorInscrito_Retorna403()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(ServidorInscrito_Retorna403));
        await ConcederAsync(SubInscrito, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        await SemearInscricaoAsync(processoId, PersonasDeTeste.FernandaDrumond, SubInscrito, SituacaoInscricao.Submetida);

        using HttpResponseMessage response = await EnviarAsync(SubInscrito, $"{Rota}/{processoId}/indicadores", MediaType);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("impedido_por_inscricao_no_processo");
    }

    private static async Task ConferirRespostaCacheEStatusAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be(MediaType);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    private static void ConferirIdentidadeEInstante(JsonElement root, Guid processoId)
    {
        Propriedades(root).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        root.GetProperty("processoSeletivoId").GetGuid().Should().Be(processoId);
        DateTimeOffset consultadoEm = root.GetProperty("consultadoEm").GetDateTimeOffset();
        consultadoEm.Offset.Should().Be(TimeSpan.Zero);
        consultadoEm.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    private static void ConferirIndicadoresSemInscricoes(JsonElement root)
    {
        JsonElement[] indicadores = [.. root.GetProperty("indicadores").EnumerateArray()];
        indicadores.Select(i => i.GetProperty("codigo").GetString()).Should().Equal("INSCRICOES", "INSCRITOS", "RASCUNHOS");
        indicadores.Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(0L, 0L, 0L);
        indicadores.Select(i => i.GetProperty("label").GetString()).Should().Equal("Inscrições", "Inscritos", "Rascunhos");
        indicadores.Select(i => i.GetProperty("descricao").GetString()).Should().Equal(
            "Submetidas e não canceladas", "Pessoas distintas com inscrição submetida e não cancelada", "Não enviados");
        indicadores.Should().AllSatisfy(item =>
            Propriedades(item).Should().BeEquivalentTo(["codigo", "valor", "label", "descricao"]));
    }

    private static void ConferirLinksEPrivacidade(JsonElement root, Guid processoId)
    {
        root.TryGetProperty("_links", out JsonElement links).Should().BeTrue(root.ToString());
        Propriedades(links).Should().BeEquivalentTo(["self", "processo"]);
        links.GetProperty("self").GetString().Should().Be($"{Rota}/{processoId}/indicadores");
        links.GetProperty("processo").GetString().Should().Be($"{Rota}/{processoId}");
        root.ToString().Should().NotContain("cpf").And.NotContain("\"sub\"").And.NotContain("nome").And.NotContain("email");
    }

    private static void ConferirStatusDeAutenticacaoEContrato(
        HttpResponseMessage anonimo,
        HttpResponseMessage autorizado,
        HttpResponseMessage tipoNaoAceito)
    {
        anonimo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        autorizado.StatusCode.Should().Be(HttpStatusCode.OK);
        tipoNaoAceito.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    private static void ConferirStatusDeEscopo(
        HttpResponseMessage semConcessao,
        HttpResponseMessage papelSemConcessao,
        HttpResponseMessage outraUnidade)
    {
        semConcessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        papelSemConcessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        outraUnidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task ConferirRascunhoAutorizadoAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("indicadores").EnumerateArray()
            .Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(0L, 0L, 0L);
    }

    private static async Task ConferirContagensAsync(HttpResponseMessage resposta, params long[] esperadas)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        using JsonDocument body = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] indicadores = [.. body.RootElement.GetProperty("indicadores").EnumerateArray()];
        indicadores.Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(esperadas);
    }

    private static string[] Propriedades(JsonElement objeto) =>
        objeto.EnumerateObject().Select(p => p.Name).ToArray();

    private static string[] Obrigatorias(JsonElement schema) =>
        schema.GetProperty("required").EnumerateArray().Select(p => p.GetString()!).ToArray();

    private static JsonElement ResolverSchema(JsonElement schemas, JsonElement schema) =>
        schema.TryGetProperty("$ref", out JsonElement reference)
            ? schemas.GetProperty(reference.GetString()!.Split('/').Last())
            : schema;

    private static bool Esquema(JsonElement schema, string type, string? format = null)
    {
        JsonElement resolved = schema;
        if (schema.TryGetProperty("$ref", out JsonElement reference))
            return false;

        return resolved.GetProperty("type").GetString() == type
            && (format is null || resolved.GetProperty("format").GetString() == format);
    }

    private async Task<Guid> SemearRascunhoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new() { Nome = $"{nome} {Guid.CreateVersion7()}" });
        return processo.Id;
    }

    private async Task<Guid> SemearPublicadoAsync(string nome, DateTimeOffset? fimDasInscricoes = null)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        if (fimDasInscricoes is not null)
            return await PublicarEditalCompletoAsync(fimDasInscricoes.Value);

        (Guid processoId, Guid documentoId) = await SemearProcessoComDocumentoAsync(nome);
        return await PublicarProcessoSemJanelaAsync(processoId, documentoId);
    }

    private async Task<Guid> PublicarProcessoSemJanelaAsync(Guid processoId, Guid documentoId)
    {
        using HttpResponseMessage publicada = await EnviarAsync(
            null, $"{Rota}/{processoId}/publicacao", null,
            new(Papel: "plataforma-admin", Metodo: HttpMethod.Post, Corpo: CorpoDaPublicacao(documentoId)));
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
        return processoId;
    }

    private async Task<Guid> PublicarEditalCompletoAsync(DateTimeOffset fimDasInscricoes)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid processoPublicadoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
            _fixture.Factory, client, fimDasInscricoes: fimDasInscricoes);
        return processoPublicadoId;
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoComDocumentoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) =
            await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new() { Nome = $"{nome} {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    private static object CorpoDaPublicacao(Guid documentoId) => new
    {
        numero = "001/2026",
        documentoEditalId = documentoId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = "EDITAL_ABERTURA",
        },
    };

    private async Task ConcederAsync(string sub, Guid processoId, string codigoPermissao)
    {
        Guid unidade = await UnidadeDoProcessoAsync(processoId);
        await ConcederNaUnidadeAsync(sub, unidade, codigoPermissao);
    }

    private async Task ConcederNaUnidadeAsync(string sub, Guid unidadeId, string codigoPermissao)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, codigoPermissao);
        await ConcessoesDeTeste.ConcederAsync(
            _fixture.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidadeId));
    }

    private async Task<Guid> UnidadeDoProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        string? sub,
        string rota,
        string? accept,
        OpcoesDeRequisicao? opcoes = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = CriarRequisicao(sub, rota, accept, opcoes);
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CriarRequisicao(
        string? sub,
        string rota,
        string? accept,
        OpcoesDeRequisicao? opcoes)
    {
        HttpRequestMessage request = new(opcoes?.Metodo ?? HttpMethod.Get, new Uri(rota, UriKind.Relative));
        if (opcoes?.Corpo is not null)
            request.Content = JsonContent.Create(opcoes.Corpo);
        ConfigurarIdentidade(request, sub, opcoes);
        if (accept is not null)
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(accept));
        ConfigurarIdempotencia(request, opcoes);
        return request;
    }

    private static void ConfigurarIdentidade(HttpRequestMessage request, string? sub, OpcoesDeRequisicao? opcoes)
    {
        if (sub is not null || opcoes?.Papel is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        if (sub is not null)
            request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        if (opcoes?.Cpf is not null)
            request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, opcoes.Cpf);
        if (opcoes?.Papel is not null)
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, opcoes.Papel);
    }

    private static void ConfigurarIdempotencia(HttpRequestMessage request, OpcoesDeRequisicao? opcoes)
    {
        if (opcoes?.Metodo is not null && opcoes.Metodo != HttpMethod.Get)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        if (opcoes?.IfMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", opcoes.IfMatch);
    }

    private sealed record OpcoesDeRequisicao(
        string? Papel = null,
        HttpMethod? Metodo = null,
        object? Corpo = null,
        string? Cpf = null,
        string? IfMatch = null);
}
