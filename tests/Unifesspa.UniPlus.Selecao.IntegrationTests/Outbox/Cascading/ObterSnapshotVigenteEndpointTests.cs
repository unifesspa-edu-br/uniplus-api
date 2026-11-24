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

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Contrato HTTP do seletor de snapshot vigente (Story #759, T6 #787,
// ADR-0075/0076/0068): GET .../snapshot-vigente resolve a publicação vigente
// num instante e projeta o snapshot congelado; 422 Snapshot.VigenteAusente
// quando não há vigente ≤ o instante, 404 quando o processo não existe.
[Collection(CascadingCollection.Name)]
public sealed class ObterSnapshotVigenteEndpointTests
{
    private readonly CascadingFixture _fixture;

    public ObterSnapshotVigenteEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "GET .../snapshot-vigente após publicar resolve o snapshot da abertura (200 + configuração congelada)")]
    public async Task ObterSnapshotVigente_AposPublicar_ResolveAbertura()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(ObterSnapshotVigente_AposPublicar_ResolveAbertura));
        (await PostPublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage response = await GetSnapshotVigenteAsync(client, processoId, instante: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        AfirmarConfiguracaoDaAbertura(root);
    }

    private static void AfirmarConfiguracaoDaAbertura(JsonElement root)
    {
        // O ato entra por VALOR — o par {id, hash} (ADR-0061). O contrato não republica
        // atributo documental algum: o documento é o ato publicado, e vive em Publicações.
        root.GetProperty("atoId").GetGuid().Should().NotBeEmpty();
        root.GetProperty("hashEdital").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("schemaVersion").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("hashConfiguracao").GetString().Should().NotBeNullOrEmpty();
        // A configuração congelada volta como objeto aninhado (não string escapada).
        JsonElement config = root.GetProperty("configuracao");
        config.ValueKind.Should().Be(JsonValueKind.Object);
        config.TryGetProperty("periodo", out _).Should().BeTrue();
        config.TryGetProperty("etapas", out _).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A unidade responsável da fase fica congelada na publicação: mudar o cadastro da unidade depois não altera o processo publicado")]
    public async Task ObterSnapshotVigente_MudancaNoCadastroDaUnidade_NaoAlteraOProcessoPublicado()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        string sufixo = Guid.CreateVersion7().ToString("N")[^8..];
        Guid unidadeId = await SemearUnidadeAsync(api, sufixo);
        UnidadeResponsavelSnapshot congelada = new(unidadeId, $"CGA-{sufixo}", $"CGA{sufixo}".ToUpperInvariant(), "Centro de Gestão Acadêmica");
        (Guid processoId, Guid documentoId) = await SemearAsync(
            api, nameof(ObterSnapshotVigente_MudancaNoCadastroDaUnidade_NaoAlteraOProcessoPublicado), congelada);
        (await PostPublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await RenomearUnidadeAsync(api, unidadeId, $"CGAX{sufixo}", "Centro de Gestão Acadêmica (renomeado)");
        await AfirmarUnidadeCongeladaAsync(api, client, processoId, congelada);
    }

    private static async Task AfirmarUnidadeCongeladaAsync(
        CascadingApiFactory api, HttpClient client, Guid processoId, UnidadeResponsavelSnapshot congelada)
    {
        HttpResponseMessage response = await GetSnapshotVigenteAsync(client, processoId, instante: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement unidade = doc.RootElement.GetProperty("configuracao")
            .GetProperty("cronogramaFases").GetProperty("fases")[0].GetProperty("unidadeResponsavel");
        unidade.GetProperty("origemId").GetGuid().Should().Be(congelada.OrigemId);
        unidade.GetProperty("codigo").GetString().Should().Be(congelada.Codigo);
        unidade.GetProperty("sigla").GetString().Should().Be(congelada.Sigla);
        unidade.GetProperty("nome").GetString().Should().Be(congelada.Nome);
        await AfirmarUnidadeGravadaAsync(api, processoId, congelada);
    }

    private static async Task AfirmarUnidadeGravadaAsync(CascadingApiFactory api, Guid processoId, UnidadeResponsavelSnapshot congelada)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        FaseCronograma fase = await db.Set<FaseCronograma>().AsNoTracking().SingleAsync(f => f.ProcessoSeletivoId == processoId);
        fase.UnidadeResponsavel.Should().Be(congelada);
    }

    [Fact(DisplayName =
        "GET .../snapshot-vigente após retificar resolve o snapshot da retificação (200)")]
    public async Task ObterSnapshotVigente_AposRetificar_ResolveRetificacao()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        (Guid processoId, Guid documentoAbertura) = await SemearAsync(api, nameof(ObterSnapshotVigente_AposRetificar_ResolveRetificacao));
        (await PostPublicarAsync(client, processoId, documentoAbertura)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);
        (await PostRetificarAsync(client, processoId, documentoRetificacao)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage response = await GetSnapshotVigenteAsync(client, processoId, instante: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await AfirmarRetificacaoAsync(api, processoId, doc.RootElement);
    }

    private static async Task AfirmarRetificacaoAsync(CascadingApiFactory api, Guid processoId, JsonElement raiz)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao esperado = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).OrderByDescending(v => v.NumeroVersao).FirstAsync();
        esperado.NumeroVersao.Should().Be(2);
        esperado.AtoCriadorRetificaId.Should().NotBeNull();
        AfirmarVersaoRetificada(raiz, esperado);
    }

    private static void AfirmarVersaoRetificada(JsonElement raiz, VersaoConfiguracao esperado)
    {
        raiz.GetProperty("hashConfiguracao").GetString().Should().Be(esperado.HashConfiguracao);
        raiz.GetProperty("snapshotPublicacaoId").GetGuid().Should().Be(esperado.Id);
        raiz.GetProperty("atoId").GetGuid().Should().Be(esperado.AtoCriadorId);
        raiz.GetProperty("hashEdital").GetString().Should().Be(esperado.AtoCriadorHash);
        raiz.TryGetProperty("natureza", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "GET .../snapshot-vigente em processo em rascunho retorna 422 Snapshot.VigenteAusente (CA-08)")]
    public async Task ObterSnapshotVigente_ProcessoEmRascunho_Retorna422()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        (Guid processoId, _) = await SemearAsync(api, nameof(ObterSnapshotVigente_ProcessoEmRascunho_Retorna422));

        HttpResponseMessage response = await GetSnapshotVigenteAsync(client, processoId, instante: null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.snapshot.vigente_ausente");
    }

    [Fact(DisplayName =
        "GET .../snapshot-vigente com instante anterior à publicação retorna 422 Snapshot.VigenteAusente")]
    public async Task ObterSnapshotVigente_InstanteAntesDaPublicacao_Retorna422()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(ObterSnapshotVigente_InstanteAntesDaPublicacao_Retorna422));
        (await PostPublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage response = await GetSnapshotVigenteAsync(
            client, processoId, instante: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.snapshot.vigente_ausente");
    }

    [Fact(DisplayName =
        "GET .../snapshot-vigente em processo inexistente retorna 404")]
    public async Task ObterSnapshotVigente_ProcessoInexistente_Retorna404()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await GetSnapshotVigenteAsync(client, Guid.CreateVersion7(), instante: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.processo_seletivo.nao_encontrado");
    }

    [Fact(DisplayName =
        "GET .../snapshot-vigente com versão de mídia vendor não suportada retorna 406 (ADR-0028)")]
    public async Task ObterSnapshotVigente_VersaoDeMidiaNaoSuportada_Retorna406()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, _) = await SemearAsync(api, nameof(ObterSnapshotVigente_VersaoDeMidiaNaoSuportada_Retorna406));
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await PedirVersaoNaoSuportadaAsync(client, processoId);
        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.contract.versao_nao_suportada");
    }

    private static async Task<HttpResponseMessage> PedirVersaoNaoSuportadaAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/snapshot-vigente", UriKind.Relative));
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.snapshot-vigente-processo-seletivo.v2+json");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetSnapshotVigenteAsync(
        HttpClient client, Guid processoId, DateTimeOffset? instante)
    {
        string rota = $"/api/selecao/processos-seletivos/{processoId}/snapshot-vigente";
        if (instante is { } valor)
        {
            rota += $"?instante={Uri.EscapeDataString(valor.ToString("O", CultureInfo.InvariantCulture))}";
        }

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        AppendTestAuth(request);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PostPublicarAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        object corpo = new { numero = "001/2026", documentoEditalId, ato = Ato("EDITAL_ABERTURA") };
        return await EnviarPublicacaoAsync(client, processoId, "publicacao", corpo);
    }

    private static async Task<HttpResponseMessage> PostRetificarAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        object corpo = new { motivo = "Correção do prazo de inscrição", numero = "001/2026-R1", documentoEditalId, ato = Ato("EDITAL_RETIFICACAO") };
        return await EnviarPublicacaoAsync(client, processoId, "retificacoes", corpo);
    }

    private static object Ato(string tipoAtoCodigo) => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        assinante = "Diretor do CEPS",
        tipoAtoCodigo,
    };

    private static async Task<HttpResponseMessage> EnviarPublicacaoAsync(HttpClient client, Guid processoId, string caminho, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/{caminho}", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static void AppendTestAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(
        CascadingApiFactory api, string nome, UnidadeResponsavelSnapshot? unidadeResponsavel = null)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}", UnidadeResponsavel = unidadeResponsavel });
        return (processo.Id, documento.Id);
    }

    private static async Task<Guid> SemearUnidadeAsync(CascadingApiFactory api, string sufixo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext org = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = Unidade.Criar(
            "Centro de Gestão Acadêmica",
            new IdentificadoresDaUnidade($"cga-{sufixo}", $"CGA{sufixo}", $"CGA-{sufixo}", null),
            new CidadeDaUnidade("1504208", "Marabá", "PA"),
            new VigenciaDaUnidade(new DateOnly(2020, 1, 1), null),
            null,
            TipoUnidade.Centro,
            true).Value!;
        org.Unidades.Add(unidade);
        await org.SaveChangesAsync().ConfigureAwait(false);
        return unidade.Id;
    }

    private static async Task RenomearUnidadeAsync(CascadingApiFactory api, Guid unidadeId, string novaSigla, string novoNome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext org = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = await org.Unidades.SingleAsync(u => u.Id == unidadeId).ConfigureAwait(false);
        unidade.Atualizar(
            novoNome,
            new MudancaDeIdentificadores(new IdentificadoresDaUnidade(unidade.Slug.Valor, novaSigla, unidade.Codigo, unidade.Alias), new DateOnly(2026, 10, 28), "Renomeada pelo cadastro"),
            new CidadeDaUnidade(unidade.CidadeCodigoIbge, unidade.CidadeNome, unidade.CidadeUf),
            unidade.VigenciaFim,
            unidade.UnidadeSuperiorId,
            unidade.Tipo,
            unidade.UnidadeAcademica)
            .IsSuccess.Should().BeTrue();
        await org.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string hashFixo = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosAPublicar.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }
}
