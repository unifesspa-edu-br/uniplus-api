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
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
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
        UnidadeResponsavelSnapshot congelada = new(unidadeId, $"CGA{sufixo}".ToUpperInvariant(), "Centro de Gestão Acadêmica");

        (Guid processoId, Guid documentoId) = await SemearAsync(
            api, nameof(ObterSnapshotVigente_MudancaNoCadastroDaUnidade_NaoAlteraOProcessoPublicado), congelada);
        (await PostPublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await RenomearUnidadeAsync(api, unidadeId, $"CGAX{sufixo}", "Centro de Gestão Acadêmica (renomeado)");

        HttpResponseMessage response = await GetSnapshotVigenteAsync(client, processoId, instante: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement unidade = doc.RootElement.GetProperty("configuracao")
            .GetProperty("cronogramaFases").GetProperty("fases")[0].GetProperty("unidadeResponsavel");

        unidade.GetProperty("origemId").GetGuid().Should().Be(unidadeId);
        unidade.GetProperty("sigla").GetString().Should().Be(congelada.Sigla);
        unidade.GetProperty("nome").GetString().Should().Be(congelada.Nome);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        FaseCronograma fase = await db.Set<FaseCronograma>().AsNoTracking().SingleAsync(f => f.ProcessoSeletivoId == processoId);
        fase.UnidadeResponsavel.Should().Be(congelada, "a fase publicada guarda a cópia de quando a unidade foi definida");
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

        // O vigente é a versão do TOPO da cadeia — a que a retificação criou. Note o que o
        // corpo NÃO carrega: não há campo dizendo "isto é uma retificação". Retificar é uma
        // relação entre atos (ADR-0103), e a relação vive no ato, que é de Publicações. O
        // contrato de leitura da Seleção publica do ato apenas o par {id, hash}.
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        VersaoConfiguracao esperado = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderByDescending(v => v.NumeroVersao)
            .FirstAsync();
        esperado.NumeroVersao.Should().Be(2, "pré-condição: a retificação sucedeu a abertura");
        esperado.AtoCriadorRetificaId.Should().NotBeNull("a versão do topo foi criada por um ato que emenda outro");

        doc.RootElement.GetProperty("hashConfiguracao").GetString().Should().Be(esperado.HashConfiguracao);
        // A referência forense durável (ADR-0075) — mesmo id do ProcessoPublicadoEvent.
        doc.RootElement.GetProperty("snapshotPublicacaoId").GetGuid().Should().Be(esperado.Id);
        // E a referência por valor ao ato: o par {id, hash} (ADR-0061).
        doc.RootElement.GetProperty("atoId").GetGuid().Should().Be(esperado.AtoCriadorId);
        doc.RootElement.GetProperty("hashEdital").GetString().Should().Be(esperado.AtoCriadorHash);
        doc.RootElement.TryGetProperty("natureza", out _).Should().BeFalse(
            "o enum de natureza não existe mais no contrato: acrescentar um tipo de ato é linha de cadastro");
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

        // O filtro de negociação roda antes do handler. O processo existe, porque o acesso
        // a ele é decidido antes da negociação. Pede uma v2 inexistente do recurso.
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/snapshot-vigente", UriKind.Relative));
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation(
            "Accept", "application/vnd.uniplus.snapshot-vigente-processo-seletivo.v2+json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.contract.versao_nao_suportada");
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
        object corpo = new
        {
            numero = "001/2026",
            documentoEditalId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = "EDITAL_ABERTURA",
            },
        };
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PostRetificarAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        object corpo = new
        {
            motivo = "Correção do prazo de inscrição",
            numero = "001/2026-R1",
            documentoEditalId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = "EDITAL_RETIFICACAO",
            },
        };
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/retificacoes", UriKind.Relative))
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
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}", unidadeResponsavel: unidadeResponsavel);
        return (processo.Id, documento.Id);
    }

    private static async Task<Guid> SemearUnidadeAsync(CascadingApiFactory api, string sufixo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext org = scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
        Unidade unidade = Unidade.Criar(
            nome: "Centro de Gestão Acadêmica",
            alias: null,
            slug: $"cga-{sufixo}",
            sigla: $"CGA{sufixo}",
            codigo: $"CGA-{sufixo}",
            unidadeSuperiorId: null,
            tipo: TipoUnidade.Centro,
            unidadeAcademica: true,
            vigenciaInicio: new DateOnly(2020, 1, 1),
            vigenciaFim: null,
            cidadeCodigoIbge: "1504208",
            cidadeNome: "Marabá",
            cidadeUf: "PA").Value!;
        org.Unidades.Add(unidade);
        await org.SaveChangesAsync().ConfigureAwait(false);
        return unidade.Id;
    }

    private static async Task RenomearUnidadeAsync(CascadingApiFactory api, Guid unidadeId, string novaSigla, string novoNome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext org = scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
        Unidade unidade = await org.Unidades.SingleAsync(u => u.Id == unidadeId).ConfigureAwait(false);
        unidade.Atualizar(
            novoNome, unidade.Alias, unidade.Slug.Valor, novaSigla, unidade.Codigo, unidade.UnidadeSuperiorId,
            unidade.Tipo, unidade.UnidadeAcademica, unidade.VigenciaFim, new DateOnly(2026, 10, 28),
            motivoMudancaIdentificador: "Renomeada pelo cadastro",
            cidadeCodigoIbge: unidade.CidadeCodigoIbge, cidadeNome: unidade.CidadeNome, cidadeUf: unidade.CidadeUf)
            .IsSuccess.Should().BeTrue();
        await org.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        string hashFixo = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosAPublicar.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }
}
