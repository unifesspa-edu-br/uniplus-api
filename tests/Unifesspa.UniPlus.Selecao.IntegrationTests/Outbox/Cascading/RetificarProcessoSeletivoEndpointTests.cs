namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
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

        Guid processoId = await SemearEPublicarAsync(api, client, nameof(Retificar_FluxoCompleto_EmiteRetificacaoEDispatchaCascading));
        Guid atoAberturaId = await RetificarAberturaAsync(api, client, processoId);
        await ConferirRetificacaoAsync(api, collector, processoId, atoAberturaId);
    }

    [Fact(DisplayName =
        "Retificação com motivo Unicode decomposto congela o mesmo valor NFC no snapshot e no ato registrado")]
    public async Task Retificar_MotivoUnicodeDecomposto_SnapshotEAtoReconciliam()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        Guid processoId = await SemearEPublicarAsync(api, client, nameof(Retificar_MotivoUnicodeDecomposto_SnapshotEAtoReconciliam));
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);
        string motivoDecomposto = "correção do prazo de inscrição".Normalize(NormalizationForm.FormD);
        await RetificarComSucessoAsync(client, processoId, documentoRetificacao, motivoDecomposto);

        await ConferirMotivoNormalizadoAsync(api, processoId);
    }

    private static async Task<Guid> SemearEPublicarAsync(CascadingApiFactory api, HttpClient client, string nome)
    {
        (Guid processoId, Guid documentoAbertura) = await SemearAsync(api, nome);
        await PublicarAberturaAsync(client, processoId, documentoAbertura);
        return processoId;
    }

    private static async Task<Guid> RetificarAberturaAsync(CascadingApiFactory api, HttpClient client, Guid processoId)
    {
        Guid atoAberturaId = await ObterAtoCriadorUnicoAsync(api, processoId);
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);
        await RetificarComSucessoAsync(client, processoId, documentoRetificacao, "Correção do prazo de inscrição");
        return atoAberturaId;
    }

    private static async Task PublicarAberturaAsync(HttpClient client, Guid processoId, Guid documento)
    {
        HttpResponseMessage publicar = await PostPublicarAsync(client, processoId, documento, MakeIdempotencyKey());
        publicar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task RetificarComSucessoAsync(HttpClient client, Guid processoId, Guid documento, string motivo)
    {
        HttpResponseMessage retificar = await PostRetificarAsync(client, processoId, documento, motivo, MakeIdempotencyKey());
        retificar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static string MotivoNoSnapshot(VersaoConfiguracao versao) => JsonNode.Parse(versao.ConfiguracaoCongelada)!
        .AsObject()["retificacao"]!["motivo"]!.GetValue<string>();

    private static async Task ConferirRetificacaoAsync(
        CascadingApiFactory api, DomainEventCollector collector, Guid processoId, Guid atoAberturaId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().HaveCount(2);
        versoes[1].AtoCriadorRetificaId.Should().Be(atoAberturaId, "a retificação emenda o ato criador da versão corrente");

        await ConferirEventoEAtoAsync(api, collector, processoId, atoAberturaId, versoes[1].AtoCriadorId);
    }

    private static async Task ConferirEventoEAtoAsync(
        CascadingApiFactory api, DomainEventCollector collector, Guid processoId, Guid atoAberturaId, Guid atoRetificacaoId)
    {
        // O evento é filtrado pelo id do ato desta retificação: a entrega cascading é
        // assíncrona e o coletor também guarda o evento da abertura (mesmo processoId).
        ProcessoPublicadoEvent? evento = await EsperarEventoPorEditalAsync(
            collector, atoRetificacaoId, TimeSpan.FromSeconds(15));
        evento.Should().NotBeNull("a retificação drena ProcessoPublicadoEvent pelo mesmo caminho cascading");
        evento!.ProcessoSeletivoId.Should().Be(processoId);

        await ConferirAtoRetificadoAsync(api, atoAberturaId, atoRetificacaoId);
    }

    private static async Task ConferirAtoRetificadoAsync(CascadingApiFactory api, Guid atoAberturaId, Guid atoRetificacaoId)
    {
        // E o ato chega a Publicações carregando a RELAÇÃO — que é o que faz dele uma
        // retificação (ADR-0103). O tipo é o que o operador DECLAROU no corpo, não algo que o
        // servidor tenha deduzido do fato de ser uma retificação: um aviso pode emendar um
        // edital, e o rótulo do documento pertence ao órgão que o publica, não ao sistema.
        AtoNormativo ato = await AtoNoRegistroCentral.LerAsync(api, atoRetificacaoId);
        ato.AtoRetificadoId.Should().Be(atoAberturaId);
        ato.MotivoRetificacao.Should().Be("Correção do prazo de inscrição");
        ato.TipoCodigo.Should().Be(
            DadosDoAtoDeTeste.TipoRetificacao,
            "o tipo do ato é o DECLARADO — o servidor não o impõe nem o deduz da relação");
    }

    private static async Task ConferirMotivoNormalizadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderByDescending(v => v.NumeroVersao)
            .FirstAsync();
        string motivoNoSnapshot = MotivoNoSnapshot(versao);

        // O snapshot forense (Seleção) e o ato registrado (Publicações) guardam o MESMO
        // valor NFC — a reconciliação entre os dois módulos vale mesmo com input decomposto
        // (Postgres não normaliza texto, e a normalização acontece uma vez só, no handler).
        AtoNormativo ato = await AtoNoRegistroCentral.LerAsync(api, versao.AtoCriadorId);
        ConferirMotivoDoAto(ato, motivoNoSnapshot);
    }

    private static void ConferirMotivoDoAto(AtoNormativo ato, string motivoNoSnapshot)
    {
        ato.MotivoRetificacao.Should().Be(motivoNoSnapshot);
        ato.MotivoRetificacao.Should().Be(
            "correção do prazo de inscrição".Normalize(NormalizationForm.FormC));
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

    [Theory(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes com motivo ausente, nulo, vazio ou em branco retorna 422 apontando o campo motivo")]
    [InlineData("ausente")]
    [InlineData("nulo")]
    [InlineData("vazio")]
    [InlineData("em branco")]
    public async Task Retificar_MotivoOmitidoOuEmBranco_Retorna422ApontandoOMotivo(string situacaoDoMotivo)
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        Guid processoId = await SemearEPublicarAsync(api, client, nameof(Retificar_MotivoOmitidoOuEmBranco_Retorna422ApontandoOMotivo));
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);
        JsonObject corpo = JsonSerializer.SerializeToNode(NovoCorpoRetificacao(documentoRetificacao, "motivo"))!.AsObject();
        DefinirMotivo(corpo, situacaoDoMotivo);

        HttpResponseMessage response = await PostRetificarCorpoAsync(client, processoId, corpo);

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            response, ("motivo", "uniplus.selecao.processo_seletivo.motivo_retificacao_obrigatorio"));
    }

    private static void DefinirMotivo(JsonObject corpo, string situacaoDoMotivo)
    {
        if (situacaoDoMotivo == "ausente")
        {
            corpo.Remove("motivo");
            return;
        }

        corpo["motivo"] = situacaoDoMotivo switch
        {
            "nulo" => null,
            "vazio" => string.Empty,
            _ => "   ",
        };
    }

    private static async Task<HttpResponseMessage> PostRetificarCorpoAsync(HttpClient client, Guid processoId, JsonObject corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/retificacoes", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static object NovoCorpoRetificacao(Guid documentoEditalId, string motivo) => new
    {
        motivo,
        numero = "001/2026-R1",
        documentoEditalId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
            Content = JsonContent.Create(ApoioDaPublicacaoDoProcessoSeletivo.NovoCorpoPublicacao(documentoEditalId, "001/2026")),
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
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
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

    private static async Task<Guid> ObterAtoCriadorUnicoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }
}
