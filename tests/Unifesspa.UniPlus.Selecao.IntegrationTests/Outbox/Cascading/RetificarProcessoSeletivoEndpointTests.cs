namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Domain.Entities;
using Domain.Events;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenário fim-a-fim da retificação (ADR-0101/0103/0108): publica via HTTP, depois
// retifica — nova versão da configuração, criada por um ato que emenda o ato criador da
// anterior, mais o cascading do ProcessoPublicadoEvent (reusado; a retificação é, em
// forma, outra emissão de ato+versão). O ato em si é registrado em Publicações, pela
// fila durável — é lá que os testes o conferem.
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RetificarProcessoSeletivoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public RetificarProcessoSeletivoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes sucede a versão, emenda o ato anterior e dispara cascading")]
    public async Task Retificar_FluxoCompleto_EmiteRetificacaoEDispatchaCascading()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();

        (Guid processoId, Guid documentoAbertura) = await SemearAsync(api, nameof(Retificar_FluxoCompleto_EmiteRetificacaoEDispatchaCascading));

        // Publica a abertura.
        HttpResponseMessage publicar = await PostPublicarAsync(client, processoId, documentoAbertura, MakeIdempotencyKey());
        publicar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid atoAberturaId = await ObterAtoCriadorUnicoAsync(api, processoId);
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);

        // Retifica: o servidor INFERE o alvo (o ato criador da versão corrente) — o cliente
        // não informa id de ato nenhum, só endereça o processo (ADR-0101).
        HttpResponseMessage retificar = await PostRetificarAsync(
            client, processoId, documentoRetificacao, "Correção do prazo de inscrição", MakeIdempotencyKey());
        retificar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().HaveCount(2);
        versoes[1].AtoCriadorRetificaId.Should().Be(atoAberturaId, "a retificação emenda o ato criador da versão corrente");

        // O evento é filtrado pelo id do ato desta retificação: a entrega cascading é
        // assíncrona e o coletor também guarda o evento da abertura (mesmo processoId).
        ProcessoPublicadoEvent? evento = await EsperarEventoPorEditalAsync(
            collector, versoes[1].AtoCriadorId, TimeSpan.FromSeconds(15));
        evento.Should().NotBeNull("a retificação drena ProcessoPublicadoEvent pelo mesmo caminho cascading");
        evento!.ProcessoSeletivoId.Should().Be(processoId);

        // E o ato chega a Publicações carregando a RELAÇÃO — que é o que faz dele uma
        // retificação (ADR-0103). O tipo é o que o operador DECLAROU no corpo, não algo que o
        // servidor tenha deduzido do fato de ser uma retificação: um aviso pode emendar um
        // edital, e o rótulo do documento pertence ao órgão que o publica, não ao sistema.
        AtoNormativo? ato = await AguardarAtoAsync(api, versoes[1].AtoCriadorId);
        ato.Should().NotBeNull();
        ato!.AtoRetificadoId.Should().Be(atoAberturaId);
        ato.MotivoRetificacao.Should().Be("Correção do prazo de inscrição");
        ato.TipoCodigo.Should().Be(
            DadosDoAtoDeTeste.TipoRetificacao,
            "o tipo do ato é o DECLARADO — o servidor não o impõe nem o deduz da relação");
    }

    [Fact(DisplayName =
        "Retificação com motivo Unicode decomposto congela o mesmo valor NFC no snapshot e no ato registrado")]
    public async Task Retificar_MotivoUnicodeDecomposto_SnapshotEAtoReconciliam()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoAbertura) = await SemearAsync(api, nameof(Retificar_MotivoUnicodeDecomposto_SnapshotEAtoReconciliam));
        HttpResponseMessage publicar = await PostPublicarAsync(client, processoId, documentoAbertura, MakeIdempotencyKey());
        publicar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);

        // Motivo em forma DECOMPOSTA (NFD): ç = c + U+0327, ã = a + U+0303. O handler deve
        // normalizar para NFC uma vez e usar o MESMO valor nos dois destinos: o bloco
        // 'retificacao' do snapshot congelado (Seleção) e o motivo do ato (Publicações).
        string motivoDecomposto = "correção do prazo de inscrição".Normalize(NormalizationForm.FormD);
        HttpResponseMessage retificar = await PostRetificarAsync(
            client, processoId, documentoRetificacao, motivoDecomposto, MakeIdempotencyKey());
        retificar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderByDescending(v => v.NumeroVersao)
            .FirstAsync();
        string motivoNoSnapshot = JsonNode.Parse(versao.ConfiguracaoCongelada)!
            .AsObject()["retificacao"]!["motivo"]!.GetValue<string>();

        // O snapshot forense (Seleção) e o ato registrado (Publicações) guardam o MESMO
        // valor NFC — a reconciliação entre os dois módulos vale mesmo com input decomposto
        // (Postgres não normaliza texto, e a normalização acontece uma vez só, no handler).
        AtoNormativo? ato = await AguardarAtoAsync(api, versao.AtoCriadorId);
        ato.Should().NotBeNull();
        ato!.MotivoRetificacao.Should().Be(motivoNoSnapshot);
        ato.MotivoRetificacao.Should().Be(
            "correção do prazo de inscrição".Normalize(NormalizationForm.FormC));
    }

    /// <summary>
    /// Espera o ato aparecer em Publicações — o registro é assíncrono, por mensagem durável
    /// (ADR-0108). A publicação em si já respondeu 204 muito antes.
    /// </summary>
    private static async Task<AtoNormativo?> AguardarAtoAsync(CascadingApiFactory api, Guid atoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < limite)
        {
            await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
            PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
            AtoNormativo? ato = await db.Set<AtoNormativo>().AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == atoId);
            if (ato is not null)
            {
                return ato;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        return null;
    }

    private static async Task<ProcessoPublicadoEvent?> EsperarEventoPorEditalAsync(
        DomainEventCollector collector, Guid editalIdEsperado, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            ProcessoPublicadoEvent? candidato = collector.Snapshot()
                .FirstOrDefault(e => e.EditalId == editalIdEsperado);
            if (candidato is not null)
            {
                return candidato;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }

        return null;
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes em processo ainda em rascunho retorna 422 TransicaoInvalida (CA-09)")]
    public async Task Retificar_ProcessoRascunho_Retorna422()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(Retificar_ProcessoRascunho_Retorna422));

        HttpResponseMessage response = await PostRetificarAsync(
            client, processoId, documentoId, "motivo", MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.transicao_invalida");
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes em processo inexistente retorna 404")]
    public async Task Retificar_ProcessoInexistente_Retorna404()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await PostRetificarAsync(
            client, Guid.CreateVersion7(), Guid.CreateVersion7(), "motivo", MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.nao_encontrado");
    }

    private static object NovoCorpoPublicacao(Guid documentoEditalId) => new
    {
        numero = "001/2026",
        periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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

    private static object NovoCorpoRetificacao(Guid documentoEditalId, string motivo) => new
    {
        motivo,
        numero = "001/2026-R1",
        periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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

    private static async Task<HttpResponseMessage> PostPublicarAsync(
        HttpClient client, Guid processoId, Guid documentoEditalId, string idempotencyKey)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(documentoEditalId)),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PostRetificarAsync(
        HttpClient client, Guid processoId, Guid documentoEditalId, string motivo, string idempotencyKey)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/retificacoes", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoRetificacao(documentoEditalId, motivo)),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

    private static void AppendTestAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        string hashFixo = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    private static async Task<Guid> ObterAtoCriadorUnicoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }
}
