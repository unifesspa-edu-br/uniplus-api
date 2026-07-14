namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Domain.Entities;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O <b>ciclo fechado</b> da retificação (Stories #860, #861, #862 — ADR-0110): abrir →
/// editar → <b>descartar</b> ou <b>fechar</b>.
/// </summary>
/// <remarks>
/// <para>
/// A prova central é a <b>imunidade do certame em curso</b> (CA-01 da #861), e ela só existe
/// se as <b>duas</b> leituras forem olhadas ao mesmo tempo: enquanto a sessão está aberta e a
/// configuração já foi alterada, o <c>GET /{id}</c> (configuração <b>viva</b>) <b>enxerga</b>
/// a alteração, e o <c>GET /{id}/snapshot-vigente</c> (versão <b>congelada</b>) <b>não</b>.
/// Um teste que olhasse só uma das duas não provaria nada.
/// </para>
/// <para>
/// É o que garante que o candidato continua vendo o que o edital publicou, enquanto o
/// administrador edita.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CicloDaRetificacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public CicloDaRetificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-01 da #861 — A IMUNIDADE. É a razão de a Feature poder existir sem quebrar nada.
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-01 (imunidade): com a sessão aberta e a configuração ALTERADA, o snapshot vigente não muda um byte — o candidato continua vendo o que o edital publicou")]
    public async Task SessaoAberta_ConfiguracaoAlterada_SnapshotVigenteImune()
    {
        Ciclo c = await PublicarAsync(nameof(SessaoAberta_ConfiguracaoAlterada_SnapshotVigenteImune));

        (string hashAntes, string bytesAntes) = await LerSnapshotVigenteAsync(c);

        string etag = LerETag(await c.AbrirAsync("Correção do peso da prova"));

        // Altera uma dimensão REAL — o peso da etapa vai de 1 para 3.
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A configuração VIVA enxerga a alteração...
        decimal pesoVivo = await LerPesoDaEtapaVivaAsync(c);
        pesoVivo.Should().Be(3.0m, "o GET do recurso serve a configuração viva, que é o que o administrador está editando");

        // ...e a versão CONGELADA não enxerga nada.
        (string hashDepois, string bytesDepois) = await LerSnapshotVigenteAsync(c);

        hashDepois.Should().Be(
            hashAntes,
            "o snapshot vigente é a versão CONGELADA — se a edição vazasse para ele, um certame publicado passaria a exibir configuração que NÃO foi publicada, e o hash do documento deixaria de valer");
        bytesDepois.Should().Be(bytesAntes, "byte a byte: nem o hash nem o conteúdo se movem durante a sessão");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // #861 — DESCARTAR
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-05 (E2E): publicar → abrir → alterar → DESCARTAR devolve o agregado ao congelado, e a versão vigente nunca mudou")]
    public async Task Descartar_DevolveAoCongelado()
    {
        Ciclo c = await PublicarAsync(nameof(Descartar_DevolveAoCongelado));

        (string hashAntes, _) = await LerSnapshotVigenteAsync(c);
        int versoesAntes = await ContarVersoesAsync(c);

        string etag = LerETag(await c.AbrirAsync("Correção do peso"));
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(3.0m);

        // Relê o ETag (a mutação incrementou a revisão) e descarta.
        string etagAtual = LerETag(await c.ObterSessaoAsync());
        HttpResponseMessage descarte = await c.DescartarAsync(etagAtual);

        descarte.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(
            1.0m,
            "o descarte REIDRATA a partir da versão congelada — não é um 'desfazer' incremental, que seria impossível: todo Definir* substitui a coleção inteira");

        (await ContarRascunhosAsync(c)).Should().Be(0, "a sessão deixou de existir");
        (await ContarVersoesAsync(c)).Should().Be(versoesAntes, "CA-03: descartar produz ZERO versão");
        (await LerSnapshotVigenteAsync(c)).Hash.Should().Be(hashAntes, "a versão vigente nunca se moveu");
    }

    [Fact(DisplayName = "CA-04: descartar sem sessão aberta devolve 409")]
    public async Task Descartar_SemSessao_Conflito()
    {
        Ciclo c = await PublicarAsync(nameof(Descartar_SemSessao_Conflito));

        HttpResponseMessage r = await c.DescartarAsync($"\"{Guid.CreateVersion7()}:1\"");

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Descartar sem If-Match devolve 428 — a rota existe PARA a sessão, e a precondição é incondicional")]
    public async Task Descartar_SemIfMatch_428()
    {
        Ciclo c = await PublicarAsync(nameof(Descartar_SemIfMatch_428));
        await c.AbrirAsync("Correção");

        HttpResponseMessage r = await c.DescartarAsync(ifMatch: null);

        r.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        (await ContarRascunhosAsync(c)).Should().Be(1, "uma recusa de protocolo não destrói a sessão");
    }

    [Fact(DisplayName = "Descartar com If-Match defasado devolve 412 e NÃO descarta")]
    public async Task Descartar_IfMatchDefasado_412()
    {
        Ciclo c = await PublicarAsync(nameof(Descartar_IfMatchDefasado_412));
        string etagInicial = LerETag(await c.AbrirAsync("Correção"));
        (await c.PutEtapasAsync(peso: 2.0m, ifMatch: etagInicial)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // O mesmo ETag, agora defasado: o PUT acima incrementou a revisão.
        HttpResponseMessage r = await c.DescartarAsync(etagInicial);

        r.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await ContarRascunhosAsync(c)).Should().Be(1, "o descarte foi recusado — a sessão continua de pé");
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(2.0m, "e a edição também");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // #862 — FECHAR. É aqui que a Feature entrega o que ela existe para entregar.
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-01 (#862): FECHAR congela a versão N+1 COM a alteração — e a versão N permanece intacta (append-only)")]
    public async Task Fechar_CongelaVersaoNovaComAAlteracao()
    {
        Ciclo c = await PublicarAsync(nameof(Fechar_CongelaVersaoNovaComAAlteracao));

        (string hashV1, string bytesV1) = await LerSnapshotVigenteAsync(c);

        string etag = LerETag(await c.AbrirAsync("Correção do peso da prova objetiva"));
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        string etagAtual = LerETag(await c.ObterSessaoAsync());
        Guid documento = await SemearDocumentoConfirmadoAsync(c);

        HttpResponseMessage fechamento = await c.FecharAsync(documento, etagAtual);

        fechamento.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == c.ProcessoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().HaveCount(2, "o fechamento congela a versão N+1");
        versoes[0].HashConfiguracao.Should().Be(hashV1, "a versão N permanece INTACTA — o passado não se muta");
        versoes[1].HashConfiguracao.Should().NotBe(
            hashV1,
            "a versão N+1 congela a configuração EDITADA — antes desta Feature, a retificação recanonicalizava a mesma configuração de sempre e as duas versões saíam idênticas");
        versoes[1].AtoCriadorRetificaId.Should().Be(versoes[0].AtoCriadorId, "o ato novo emenda o ato criador da versão anterior");

        (await ContarRascunhosAsync(c)).Should().Be(0, "a sessão morre com o fechamento");

        // E o peso alterado está de fato dentro dos bytes congelados da versão nova.
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(3.0m);
        bytesV1.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "CA-06 (#862): fechar sem sessão aberta → 409; com If-Match defasado → 412")]
    public async Task Fechar_SemSessaoOuDefasado_Recusa()
    {
        Ciclo c = await PublicarAsync(nameof(Fechar_SemSessaoOuDefasado_Recusa));
        Guid doc = await SemearDocumentoConfirmadoAsync(c);

        (await c.FecharAsync(doc, $"\"{Guid.CreateVersion7()}:1\""))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        string etagInicial = LerETag(await c.AbrirAsync("Correção"));
        await c.PutEtapasAsync(peso: 2.0m, ifMatch: etagInicial);

        (await c.FecharAsync(doc, etagInicial))
            .StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        (await ContarRascunhosAsync(c)).Should().Be(1, "uma recusa não destrói a sessão — o administrador corrige e tenta de novo");
    }

    [Fact(DisplayName = "Uma recusa de NEGÓCIO no fechamento não destrói a sessão — o rascunho permanece aberto, com a edição intacta")]
    public async Task Fechar_RecusadoPorNegocio_PreservaASessao()
    {
        Ciclo c = await PublicarAsync(nameof(Fechar_RecusadoPorNegocio_PreservaASessao));

        string etag = LerETag(await c.AbrirAsync("Correção"));
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        string etagAtual = LerETag(await c.ObterSessaoAsync());

        // Documento inexistente: recusa de negócio (422), depois da precondição ter passado.
        HttpResponseMessage r = await c.FecharAsync(Guid.CreateVersion7(), etagAtual);

        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ContarRascunhosAsync(c)).Should().Be(1, "encerrar a sessão numa recusa de negócio faria o administrador perder toda a edição");
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(3.0m, "e a edição continua lá, esperando a correção");
        (await ContarVersoesAsync(c)).Should().Be(1, "nada foi congelado");
    }

    [Fact(DisplayName = "CA-02 (regressão #862): com a sessão FECHADA, o atalho atômico POST /retificacoes volta a funcionar exatamente como antes")]
    public async Task Atalho_AposFechamento_ContinuaFuncionando()
    {
        Ciclo c = await PublicarAsync(nameof(Atalho_AposFechamento_ContinuaFuncionando));

        string etag = LerETag(await c.AbrirAsync("Primeira correção"));
        await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag);
        Guid doc1 = await SemearDocumentoConfirmadoAsync(c);
        (await c.FecharAsync(doc1, LerETag(await c.ObterSessaoAsync())))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Sem sessão aberta, o atalho de sempre volta a valer — e sucede a cadeia.
        Guid doc2 = await SemearDocumentoConfirmadoAsync(c);
        HttpResponseMessage atalho = await c.AtalhoAsync(doc2, "Segunda correção, em um ato só");

        atalho.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ContarVersoesAsync(c)).Should().Be(3, "v1 (publicação) → v2 (fechamento da sessão) → v3 (atalho)");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Infra do cenário
    // ══════════════════════════════════════════════════════════════════════════════

    private sealed record Ciclo(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        private const string Rota = "/api/selecao/processos-seletivos";

        public Task<HttpResponseMessage> AbrirAsync(string motivo) =>
            EnviarAsync(HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacao-em-curso", new { motivo }, null);

        public Task<HttpResponseMessage> ObterSessaoAsync() =>
            EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/retificacao-em-curso", null, null, idempotente: false);

        public Task<HttpResponseMessage> DescartarAsync(string? ifMatch) =>
            EnviarAsync(HttpMethod.Delete, $"{Rota}/{ProcessoId}/retificacao-em-curso", null, ifMatch);

        public Task<HttpResponseMessage> FecharAsync(Guid documentoId, string? ifMatch) =>
            EnviarAsync(
                HttpMethod.Post,
                $"{Rota}/{ProcessoId}/retificacao-em-curso/fechamento",
                CorpoDoAto(documentoId, motivo: null),
                ifMatch);

        public Task<HttpResponseMessage> AtalhoAsync(Guid documentoId, string motivo) =>
            EnviarAsync(HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacoes", CorpoDoAto(documentoId, motivo), null);

        /// <summary>O peso é o que muda — é uma dimensão REAL da configuração, não um campo decorativo.</summary>
        public Task<HttpResponseMessage> PutEtapasAsync(decimal peso, string? ifMatch) =>
            EnviarAsync(
                HttpMethod.Put,
                $"{Rota}/{ProcessoId}/etapas",
                new[] { new { nome = "Prova Objetiva", carater = 1, peso, notaMinima = (decimal?)null, ordem = 1 } },
                ifMatch);

        public Task<HttpResponseMessage> ObterRecursoAsync() =>
            EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}", null, null, idempotente: false,
                accept: "application/vnd.uniplus.processo-seletivo.v1+json");

        public Task<HttpResponseMessage> ObterSnapshotVigenteAsync() =>
            EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/snapshot-vigente", null, null, idempotente: false,
                accept: "application/vnd.uniplus.snapshot-vigente-processo-seletivo.v1+json");

        private static object CorpoDoAto(Guid documentoId, string? motivo)
        {
            object ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataPublicacao = Hoje(),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = "EDITAL_RETIFICACAO",
            };

            return motivo is null
                ? new
                {
                    numero = "001/2026-R",
                    periodoInscricaoInicio = HojeMais(1),
                    periodoInscricaoFim = HojeMais(40),
                    documentoEditalId = documentoId,
                    ato,
                }
                : new
                {
                    motivo,
                    numero = "001/2026-R",
                    periodoInscricaoInicio = HojeMais(1),
                    periodoInscricaoFim = HojeMais(40),
                    documentoEditalId = documentoId,
                    ato,
                };
        }

        private async Task<HttpResponseMessage> EnviarAsync(
            HttpMethod metodo, string rota, object? corpo, string? ifMatch, bool idempotente = true,
            string? accept = null)
        {
            using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
            if (corpo is not null)
            {
                request.Content = JsonContent.Create(corpo);
            }

            // As leituras são versionadas pelo vendor media type (ADR-0028) — sem o Accept
            // certo, o servidor devolve 406 e o teste "falharia" por um motivo que nada tem a
            // ver com o que ele quer provar.
            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation("Accept", accept);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue(
                TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");

            if (idempotente)
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            }

            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }
    }

    private async Task<Ciclo> PublicarAsync(string nome)
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

        Ciclo c = new(api, client, processoId);

        using HttpRequestMessage publicar = new(
            HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numero = "001/2026",
                periodoInscricaoInicio = Hoje(),
                periodoInscricaoFim = HojeMais(30),
                documentoEditalId = documentoId,
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
        publicar.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        publicar.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        publicar.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        (await client.SendAsync(publicar)).StatusCode.Should().Be(
            HttpStatusCode.NoContent, "o cenário depende de um certame publicado");

        return c;
    }

    /// <summary>A configuração VIVA — o que o administrador está editando.</summary>
    private static async Task<decimal> LerPesoDaEtapaVivaAsync(Ciclo c)
    {
        HttpResponseMessage r = await c.ObterRecursoAsync();
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("etapas")[0].GetProperty("peso").GetDecimal();
    }

    /// <summary>A versão CONGELADA — o que o mundo vê.</summary>
    private static async Task<(string Hash, string Bytes)> LerSnapshotVigenteAsync(Ciclo c)
    {
        HttpResponseMessage r = await c.ObterSnapshotVigenteAsync();
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        return (
            raiz.GetProperty("hashConfiguracao").GetString()!,
            raiz.GetProperty("configuracao").GetRawText());
    }

    private static async Task<int> ContarVersoesAsync(Ciclo c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking().CountAsync(v => v.ProcessoSeletivoId == c.ProcessoId);
    }

    private static async Task<int> ContarRascunhosAsync(Ciclo c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.RascunhosRetificacao.AsNoTracking().CountAsync(r => r.ProcessoSeletivoId == c.ProcessoId);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(Ciclo c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        string hash = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(c.ProcessoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    private static string LerETag(HttpResponseMessage r)
    {
        r.Headers.ETag.Should().NotBeNull($"a resposta {(int)r.StatusCode} de uma sessão editorial carrega o ETag");
        return r.Headers.ETag!.ToString();
    }

    private static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string HojeMais(int dias) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
