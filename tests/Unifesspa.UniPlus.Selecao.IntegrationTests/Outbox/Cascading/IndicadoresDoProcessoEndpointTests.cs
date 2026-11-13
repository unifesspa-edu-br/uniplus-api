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
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
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

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be(MediaType);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;
        Propriedades(root).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        root.GetProperty("processoSeletivoId").GetGuid().Should().Be(processoId);
        DateTimeOffset consultadoEm = root.GetProperty("consultadoEm").GetDateTimeOffset();
        consultadoEm.Offset.Should().Be(TimeSpan.Zero);
        consultadoEm.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        root.GetProperty("indicadores").GetArrayLength().Should().Be(3);
        JsonElement.ArrayEnumerator itens = root.GetProperty("indicadores").EnumerateArray();
        JsonElement[] indicadores = [.. itens];
        indicadores.Select(i => i.GetProperty("codigo").GetString()).Should()
            .Equal("INSCRICOES", "INSCRITOS", "RASCUNHOS");
        indicadores.Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(0L, 0L, 0L);
        indicadores.Select(i => i.GetProperty("label").GetString()).Should()
            .Equal("Inscrições", "Inscritos", "Rascunhos");
        indicadores.Select(i => i.GetProperty("descricao").GetString()).Should().Equal(
            "Submetidas e não canceladas",
            "Pessoas distintas com inscrição submetida e não cancelada",
            "Inscrições ainda não enviadas");
        indicadores.Should().AllSatisfy(item =>
        {
            Propriedades(item).Should().BeEquivalentTo(["codigo", "valor", "label", "descricao"]);
        });
        root.TryGetProperty("_links", out JsonElement links).Should().BeTrue(root.ToString());
        Propriedades(links).Should().BeEquivalentTo(["self", "processo"]);
        links.GetProperty("self").GetString().Should().Be($"{Rota}/{processoId}/indicadores");
        links.GetProperty("processo").GetString().Should().Be($"{Rota}/{processoId}");
        root.ToString().Should().NotContain("cpf").And.NotContain("\"sub\"")
            .And.NotContain("nome").And.NotContain("email").And.NotContain("inscricaoId");
    }

    [Fact(DisplayName = "GET de indicadores exige autenticação, negociação v1 e autorização com escopo na unidade dona")]
    public async Task AutenticacaoNegociacaoEEscopo_SeguemContrato()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(AutenticacaoNegociacaoEEscopo_SeguemContrato));
        Guid outraUnidade = Guid.CreateVersion7();
        await ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        await ConcederNaUnidadeAsync("indicadores.outra.unidade", outraUnidade, PermissaoSelecao.ProcessosAcompanhar.Codigo);

        using HttpResponseMessage anonimo = await EnviarAsync(null, $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage autorizado = await EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage semConcessao = await EnviarAsync("indicadores.sem.concessao", $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage papelSemConcessao = await EnviarAsync("indicadores.admin", $"{Rota}/{processoId}/indicadores", MediaType, "plataforma-admin");
        using HttpResponseMessage outraUnidadeResponse = await EnviarAsync("indicadores.outra.unidade", $"{Rota}/{processoId}/indicadores", MediaType);
        using HttpResponseMessage tipoNaoAceito = await EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores",
            "application/vnd.uniplus.indicadores-do-processo.v9+json");

        anonimo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        autorizado.StatusCode.Should().Be(HttpStatusCode.OK);
        semConcessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        papelSemConcessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        outraUnidadeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        tipoNaoAceito.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
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

        configurador.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument body = JsonDocument.Parse(await configurador.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("indicadores").EnumerateArray()
                .Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(0L, 0L, 0L);
        }

        acompanha.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Inscrição submetida conta uma vez; cancelamento, rascunho e comprovantes não criam indicadores extras")]
    public async Task Contagens_ConsideramSomenteEstadosAtuaisDaInscricao()
    {
        // A correção ainda não enviada não é um estado persistido: a edição fica no cliente e
        // só altera a mesma inscrição quando um novo comprovante é emitido. Não há fixture
        // legítima de "correção pendente" distinta de uma inscrição submetida.
        Guid processoId = await SemearPublicadoAsync(
            nameof(Contagens_ConsideramSomenteEstadosAtuaisDaInscricao),
            new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero));
        await SemearInscricoesAsync(processoId);
        await DescartarRascunhoPelaApiAsync(processoId);
        await ConcederAsync(Pessoa, processoId, PermissaoSelecao.ProcessosAcompanhar.Codigo);

        using HttpResponseMessage response = await EnviarAsync(Pessoa, $"{Rota}/{processoId}/indicadores", MediaType);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement[] indicadores = [.. body.RootElement.GetProperty("indicadores").EnumerateArray()];
        indicadores.Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(2L, 2L, 2L);
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
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private async Task<Guid> SemearPublicadoAsync(string nome, DateTimeOffset? fimDasInscricoes = null)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        if (fimDasInscricoes is not null)
        {
            using HttpClient clientDaPublicacaoCompleta = _fixture.Factory.CreateClient();
            (Guid processoPublicadoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
                _fixture.Factory, clientDaPublicacaoCompleta, fimDasInscricoes: fimDasInscricoes);
            return processoPublicadoId;
        }

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoAPublicar documento) =
                await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage publicada = await EnviarAsync(
            null, $"{Rota}/{processoId}/publicacao", null, "plataforma-admin", HttpMethod.Post,
            new
            {
                numero = "001/2026",
                documentoEditalId = documentoId,
                ato = new
                {
                    orgao = "CEPS",
                    serie = "EDITAL",
                    ano = 2026,
                    dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    assinante = "Diretor do CEPS",
                    tipoAtoCodigo = "EDITAL_ABERTURA",
                },
            });
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
        return processoId;
    }

    private async Task SemearInscricoesAsync(Guid processoId)
    {
        // O mesmo sub em emissores distintos e CPFs distintos diferencia a identidade canônica
        // por CPF de uma contagem por sub. O índice parcial impede semear duas ativas do mesmo CPF.
        await SemearInscricaoAsync(processoId, PersonasDeTeste.FernandaDrumond, "same-sub", SituacaoInscricao.Submetida);
        await SemearInscricaoAsync(processoId, PersonasDeTeste.FernandaDrumond, "candidato.antiga", SituacaoInscricao.Cancelada);
        await SemearInscricaoAsync(
            processoId, PersonasDeTeste.KevinCarvalho, "same-sub", SituacaoInscricao.Submetida,
            "https://outro-emissor.exemplo/realms/outro");
        await SemearInscricaoAsync(
            processoId, PersonasDeTeste.ErickIsaacRamos, "candidato.rascunho.ativo", SituacaoInscricao.Rascunho);
        await SemearInscricaoAsync(
            processoId, Cpf.Criar(PersonasDeTeste.Candidatas[2].Cpf).Value!, "candidato.rascunho", SituacaoInscricao.Rascunho);

    }

    private async Task DescartarRascunhoPelaApiAsync(Guid processoId)
    {
        const string sub = "indicadores.rascunho.descartado.pela.api";
        Cpf cpf = Cpf.Criar(PersonasDeTeste.Candidatas[4].Cpf).Value!;
        string rota = $"{Rota}/{processoId}/inscricao";

        using HttpResponseMessage iniciado = await EnviarAsync(sub, rota, "application/vnd.uniplus.inscricao.v1+json",
            cpf: cpf.Valor, metodo: HttpMethod.Post);
        iniciado.StatusCode.Should().Be(HttpStatusCode.OK, await iniciado.Content.ReadAsStringAsync());

        using HttpResponseMessage leitura = await EnviarAsync(sub, rota, "application/vnd.uniplus.inscricao.v1+json", cpf: cpf.Valor);
        leitura.StatusCode.Should().Be(HttpStatusCode.OK, await leitura.Content.ReadAsStringAsync());
        using JsonDocument inscricao = JsonDocument.Parse(await leitura.Content.ReadAsStringAsync());
        string etag = inscricao.RootElement.GetProperty("eTag").GetString()!;

        using HttpResponseMessage descartado = await EnviarAsync(
            sub,
            rota,
            "application/vnd.uniplus.inscricao.v1+json",
            cpf: cpf.Valor,
            metodo: HttpMethod.Delete,
            ifMatch: etag);
        descartado.StatusCode.Should().Be(HttpStatusCode.NoContent, await descartado.Content.ReadAsStringAsync());
    }

    private async Task<Guid> SemearInscricaoAsync(
        Guid processoId,
        Cpf cpf,
        string sub,
        SituacaoInscricao situacao,
        string emissor = TestAuthHandler.DefaultIssuer)
    {
        Guid id = Guid.CreateVersion7();
        bool cancelada = situacao == SituacaoInscricao.Cancelada;
        Inscricao inscricao = Inscricao.Reidratar(
            id, processoId, cpf, sub, emissor, situacao,
            Guid.CreateVersion7(), null, null,
            situacao == SituacaoInscricao.Rascunho ? null : "000001",
            cancelada ? DateTimeOffset.UtcNow : null,
            cancelada ? sub : null,
            cancelada ? "a pedido do candidato" : null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["CPF"] = JsonSerializer.SerializeToElement(cpf.Valor),
            },
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            [],
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
            {
                ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
            },
            revisao: 1, DateTimeOffset.UtcNow, null);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IInscricaoRepository>().AdicionarAsync(inscricao);
        if (situacao == SituacaoInscricao.Submetida)
        {
            IInscricaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IInscricaoRepository>();
            foreach (string numero in new[] { "000001", "000002" })
            {
                await repositorio.AdicionarComprovanteAsync(ComprovanteInscricao.Emitir(
                    inscricao.Id, numero, DateTimeOffset.UtcNow, null,
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                    new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
                    [], []));
            }
        }

        await scope.ServiceProvider.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();
        return id;
    }

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
        return await scope.ServiceProvider.GetRequiredService<SelecaoDbContext>().ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        string? sub,
        string rota,
        string? accept,
        string? papel = null,
        HttpMethod? metodo = null,
        object? corpo = null,
        string? cpf = null,
        string? ifMatch = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(metodo ?? HttpMethod.Get, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
            request.Content = JsonContent.Create(corpo);
        if (sub is not null || papel is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        if (sub is not null)
            request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        if (cpf is not null)
            request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, cpf);
        if (papel is not null)
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        if (accept is not null)
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(accept));
        if (metodo is not null && metodo != HttpMethod.Get)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await client.SendAsync(request);
    }
}
