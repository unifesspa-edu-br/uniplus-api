namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Os fatos das versões publicadas na retificação, fim a fim pelo HTTP e com versões de verdade
/// (UNI-REQ-0144): a primeira versão publica a inscrição com <c>QUILOMBOLA</c>, uma retificação
/// fechada acrescenta <c>BAIXA_RENDA</c>, e a sessão seguinte é aberta sobre as duas. Prova o que só
/// o ciclo real prova: a abertura lê a cadeia inteira de envelopes, o rascunho persiste o que ela
/// leu, e as recusas chegam ao cliente com o código delas.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FatosDasVersoesPublicadasNaRetificacaoEndpointTests
{
    // Os itens do processo, depois da seção do conjunto básico, que ocupa as primeiras ordens.
    private static readonly int PrimeiraOrdem = ConjuntoBasicoDaInscricao.Itens.Count;

    private readonly CascadingFixture _fixture;

    public FatosDasVersoesPublicadasNaRetificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Item da habilitação acrescentada na retificação cita fato da inscrição de todas as versões; o que só a última coleta é 422")]
    public async Task Habilitacao_CitaFatoAusenteDeVersaoPublicada_422()
    {
        Contexto ctx = await DuasVersoesPublicadasAsync(nameof(Habilitacao_CitaFatoAusenteDeVersaoPublicada_422));
        string etag = await ctx.AbrirAsync();
        etag = LerETag(await Aceito(ctx.PutFormularioDeHabilitacaoAsync(etag)));

        HttpResponseMessage recusa = await ctx.PutItensAsync("HABILITACAO", [ItemDaHabilitacaoQueCita("BAIXA_RENDA")], etag);

        await DeveRecusarAsync(recusa, "uniplus.selecao.retificacao.fato_ausente_de_versao_publicada");
        (await ctx.PutItensAsync("HABILITACAO", [ItemDaHabilitacaoQueCita("QUILOMBOLA")], etag))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "QUILOMBOLA é coletado pela inscrição nas duas versões");
    }

    private static object ItemDaInscricao(string codigo, int posicao) => new
    {
        fatoCodigo = codigo,
        ordem = PrimeiraOrdem + posicao,
        rotulo = codigo,
        tipoRenderizacao = "BOOLEANO",
        obrigatoriedade = "NUNCA",
        precondicao = (object?)null,
        etapaCodigo = FormularioDeInscricaoHttp.Secao,
    };

    private static object ItemDaHabilitacaoQueCita(string citado) => new
    {
        fatoCodigo = "CONCORRER_PCD",
        ordem = 0,
        rotulo = "Concorrer às vagas para pessoas com deficiência",
        tipoRenderizacao = "BOOLEANO",
        obrigatoriedade = "NUNCA",
        precondicao = new[] { new[] { new { fato = citado, operador = "IGUAL", valor = true } } },
        etapaCodigo = "DADOS",
    };

    private static async Task DeveRecusarAsync(HttpResponseMessage resposta, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("code").GetString().Should().Be(codigo, corpo);
    }

    /// <summary>
    /// Publica a inscrição com <c>QUILOMBOLA</c> e fecha uma retificação que acrescenta
    /// <c>BAIXA_RENDA</c>: duas versões, e a segunda é a vigente.
    /// </summary>
    private async Task<Contexto> DuasVersoesPublicadasAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);

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

        Contexto ctx = new(api, api.CreateClient(), processoId);
        (await ctx.PutItensAsync("INSCRICAO", [ItemDaInscricao("QUILOMBOLA", 0)], ifMatch: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.EnviarAsync(HttpMethod.Post, "publicacao", CorpoDoAto(documentoId, "EDITAL_ABERTURA", "001/2026"), ifMatch: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "o cenário depende de um certame publicado");

        string etag = await ctx.AbrirAsync();
        etag = LerETag(await Aceito(ctx.PutItensAsync("INSCRICAO", [ItemDaInscricao("QUILOMBOLA", 0), ItemDaInscricao("BAIXA_RENDA", 1)], etag)));
        Guid documentoDaRetificacao = await SemearDocumentoConfirmadoAsync(ctx);
        HttpResponseMessage fechamento = await ctx.EnviarAsync(
            HttpMethod.Post, "retificacao-em-curso/fechamento", CorpoDoAto(documentoDaRetificacao, "EDITAL_RETIFICACAO", "001/2026-R"), etag);
        fechamento.StatusCode.Should().Be(HttpStatusCode.NoContent, await fechamento.Content.ReadAsStringAsync());
        return ctx;
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(ctx.ProcessoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, string.Concat(Enumerable.Repeat("cd45670189", 7))[..64], TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    private static object CorpoDoAto(Guid documentoId, string tipoAto, string numero) => new
    {
        numero,
        documentoEditalId = documentoId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = tipoAto,
        },
    };

    private static async Task<HttpResponseMessage> Aceito(Task<HttpResponseMessage> chamada)
    {
        HttpResponseMessage resposta = await chamada;
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        return resposta;
    }

    private static string LerETag(HttpResponseMessage resposta)
    {
        resposta.Headers.ETag.Should().NotBeNull($"a resposta {(int)resposta.StatusCode} de uma sessão editorial carrega o ETag");
        return resposta.Headers.ETag!.ToString();
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<string> AbrirAsync()
        {
            HttpResponseMessage resposta = await EnviarAsync(HttpMethod.Post, "retificacao-em-curso", new { motivo = "Correção da coleta" }, ifMatch: null)
                .ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
            return LerETag(resposta);
        }

        public Task<HttpResponseMessage> PutItensAsync(string finalidade, IReadOnlyList<object> itens, string? ifMatch) =>
            EnviarAsync(HttpMethod.Put, new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/{finalidade}/itens", UriKind.Relative), new { itens }, ifMatch);

        /// <summary>O formulário de habilitação, que a retificação acrescenta, ainda sem fase.</summary>
        public Task<HttpResponseMessage> PutFormularioDeHabilitacaoAsync(string ifMatch) =>
            EnviarAsync(
                HttpMethod.Put,
                new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/HABILITACAO", UriKind.Relative),
                new
                {
                    faseId = (Guid?)null,
                    titulo = "Habilitação",
                    etapas = new object[]
                    {
                        new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, titulo = "Dados" },
                        new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", titulo = "Revisão e aceite" },
                    },
                },
                ifMatch);

        public Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string recurso, object corpo, string? ifMatch) =>
            EnviarAsync(metodo, new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/{recurso}", UriKind.Relative), corpo, ifMatch);

        private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, Uri rota, object corpo, string? ifMatch)
        {
            using HttpRequestMessage request = new(metodo, rota) { Content = JsonContent.Create(corpo) };
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }
    }
}
