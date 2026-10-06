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
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Os formulários por finalidade (UNI-REQ-0144), fim a fim pelo HTTP: leitura pública sob
/// <c>/processos-seletivos/{id}/formularios/{finalidade}</c> e escrita administrativa sob
/// <c>/admin/processos-seletivos/{id}/formularios/{finalidade}</c> (ADR-0064).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FormulariosEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FormulariosEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "PUT admin sem autenticação é 401")]
    public async Task Definir_SemAutenticacao_401()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_SemAutenticacao_401));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Título", autenticar: Autenticacao.Nenhuma);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "PUT admin autenticado sem o papel plataforma-admin é 403")]
    public async Task Definir_SemPapel_403()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_SemPapel_403));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Título", autenticar: Autenticacao.PapelErrado);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "PUT admin em rascunho é 204 sem ETag e persiste o título")]
    public async Task Definir_EmRascunho_204SemEtagEPersiste()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_EmRascunho_204SemEtagEPersiste));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync("Formulário de Inscrição");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resposta.Headers.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.Set<ProcessoSeletivo>().AsNoTracking().Include(static p => p.Formularios)
            .SingleAsync(p => p.Id == ctx.ProcessoId);
        processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Titulo.Should().Be("Formulário de Inscrição");
    }

    [Fact(DisplayName = "PUT admin com título acima do limite é 422 com a violação em errors[]")]
    public async Task Definir_TituloAcimaDoLimite_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_TituloAcimaDoLimite_422));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(new string('a', 301));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        // field usa o mesmo casing do payload JSON (camelCase, ADR-0023), não o PascalCase do C#.
        JsonElement erro = doc.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be("titulo");
        erro.GetProperty("code").GetString().Should().Be("uniplus.estrutura_formulario.titulo_tamanho");
    }

    [Fact(DisplayName = "Finalidade fora do vocabulário é 404, na leitura e na escrita")]
    public async Task FinalidadeDesconhecida_404()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(FinalidadeDesconhecida_404));

        HttpResponseMessage escrita = await ctx.PutFormularioAsync("Título", finalidade: "MATRICULA");
        HttpResponseMessage leitura = await ctx.GetFormularioAsync(finalidade: "MATRICULA");

        escrita.StatusCode.Should().Be(HttpStatusCode.NotFound);
        leitura.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Formulário de habilitação fora da fase de habilitação é 422")]
    public async Task Definir_FaseIncoerenteComFinalidade_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Definir_FaseIncoerenteComFinalidade_422));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Habilitação", finalidade: "HABILITACAO", faseId: await ctx.FaseDeInscricaoAsync());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.fase_incoerente_com_finalidade");
    }

    [Fact(DisplayName = "GET público de processo em rascunho responde o MESMO que de processo inexistente")]
    public async Task Obter_ProcessoEmRascunho_RespondeIgualAoInexistente()
    {
        // A renderização é anônima. Enquanto ela distinguia rascunho (422) de inexistente (404),
        // respondia a um estranho, numa requisição, se um identificador corresponde a um processo
        // que ainda não é público. Resolvendo pela divulgação, os dois deixam de ser distinguíveis
        // — nenhum dos dois tem linha.
        Contexto ctx = await SemearRascunhoAsync(nameof(Obter_ProcessoEmRascunho_RespondeIgualAoInexistente));

        HttpResponseMessage emRascunho = await ctx.GetFormularioAsync();
        HttpResponseMessage inexistente = await ctx.Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{Guid.CreateVersion7()}/formularios/INSCRICAO", UriKind.Relative));

        emRascunho.StatusCode.Should().Be(HttpStatusCode.NotFound);
        emRascunho.StatusCode.Should().Be(
            inexistente.StatusCode, "a resposta não pode separar o que ainda não é público do que não existe");
    }

    [Fact(DisplayName = "GET admin do rascunho exige autenticação e o papel plataforma-admin")]
    public async Task ObterRascunho_SemPapel_Recusa()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ObterRascunho_SemPapel_Recusa));

        (await ctx.GetRascunhoAsync(Autenticacao.Nenhuma)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ctx.GetRascunhoAsync(Autenticacao.PapelErrado)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "GET admin do rascunho serve o formulário da configuração viva, que o público ainda não serve")]
    public async Task ObterRascunho_EmRascunho_200ComApresentacaoERegras()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ObterRascunho_EmRascunho_200ComApresentacaoERegras));
        (await ctx.PutFormularioAsync("Formulário de Inscrição")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.PutFatosAsync([])).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage publico = await ctx.GetFormularioAsync();
        HttpResponseMessage rascunho = await ctx.GetRascunhoAsync(Autenticacao.PlataformaAdmin);

        publico.StatusCode.Should().Be(HttpStatusCode.NotFound, "o certame ainda não foi divulgado");
        rascunho.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await rascunho.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("titulo").GetString().Should().Be("Formulário de Inscrição");
        root.GetProperty("fatosColetados").GetArrayLength().Should().Be(ConjuntoBasicoDaInscricao.Itens.Count);
        root.GetProperty("regras").GetProperty("etapas").EnumerateArray().SelectMany(static e => e.GetProperty("itens").EnumerateArray())
            .Select(static i => i.GetProperty("fatoCodigo").GetString())
            .Should().Contain("COR_RACA", "as regras da configuração viva vão com a apresentação");
        root.GetProperty("comprovacaoDocumental").ValueKind.Should().Be(JsonValueKind.Null, "a lista de documentos sai da publicação");
    }

    [Fact(DisplayName = "GET público, sem autenticação, retorna 404 para processo inexistente")]
    public async Task Obter_ProcessoInexistente_404()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Obter_ProcessoInexistente_404));

        HttpResponseMessage resposta = await ctx.Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{Guid.NewGuid()}/formularios/INSCRICAO", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET público, sem autenticação, retorna 200 com título/termo/fatos para processo publicado")]
    public async Task Obter_ProcessoPublicado_200ComFormularioEFatos()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Obter_ProcessoPublicado_200ComFormularioEFatos));

        (await ctx.PutFormularioAsync("Formulário de Inscrição"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.PutFatosAsync([])).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ctx.PublicarAsync();

        // O formulário segue a divulgação, que chega pela fila durável — o 204 da publicação volta
        // muito antes dela existir. Sem esta espera, o GET corre contra a materialização e o teste
        // mede a corrida, não o contrato.
        await ctx.EsperarDivulgacaoAsync();

        HttpResponseMessage resposta = await ctx.GetFormularioAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("finalidade").GetString().Should().Be("INSCRICAO");
        root.GetProperty("titulo").GetString().Should().Be("Formulário de Inscrição");
        root.GetProperty("etapas").EnumerateArray().Select(static e => e.GetProperty("codigo").GetString())
            .Should().Equal(ConjuntoBasicoDaInscricao.CodigoDaSecao, "DADOS", "REVISAO");
        root.GetProperty("termos").GetArrayLength().Should().Be(0);
        JsonElement fatos = root.GetProperty("fatosColetados");
        fatos.GetArrayLength().Should().Be(ConjuntoBasicoDaInscricao.Itens.Count);
        JsonElement fato = fatos.EnumerateArray().Single(static f => f.GetProperty("fatoCodigo").GetString() == "COR_RACA");
        fato.GetProperty("fatoCodigo").GetString().Should().Be("COR_RACA");
        fato.GetProperty("rotulo").GetString().Should().Be("Cor ou raça");
        fato.GetProperty("tipoRenderizacao").GetString().Should().Be("SELECAO_UNICA");
        fato.GetProperty("etapaCodigo").GetString().Should().Be(ConjuntoBasicoDaInscricao.CodigoDaSecao);
        fato.TryGetProperty("obrigatoriedade", out _).Should().BeFalse("as regras do campo estão só nas regras do formulário");
        JsonElement regraDoCampo = root.GetProperty("regras").GetProperty("etapas").EnumerateArray()
            .SelectMany(static e => e.GetProperty("itens").EnumerateArray())
            .Single(static i => i.GetProperty("fatoCodigo").GetString() == "COR_RACA");
        regraDoCampo.GetProperty("obrigatoriedade").GetString().Should().Be("SEMPRE");
        regraDoCampo.GetProperty("oferta").GetArrayLength().Should().BePositive("a resposta fora da oferta congelada não vale");
        root.GetProperty("pressupostos").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "GET público exige revalidação, inclusive na recusa anterior à divulgação")]
    public async Task Obter_AntesEDepoisDaDivulgacao_ExigeRevalidacao()
    {
        // O 404 anterior à divulgação é transitório: o mesmo endereço passa a servir o formulário
        // assim que a materialização chega. Sem diretiva, um cache compartilhado atribui frescor
        // heurístico à recusa e segue escondendo o formulário já público.
        Contexto ctx = await SemearRascunhoAsync(nameof(Obter_AntesEDepoisDaDivulgacao_ExigeRevalidacao));

        (await ctx.PutFormularioAsync("Formulário de Inscrição"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage recusa = await ctx.GetFormularioAsync();

        recusa.StatusCode.Should().Be(HttpStatusCode.NotFound);
        recusa.Headers.CacheControl!.NoCache.Should().BeTrue(
            "a recusa é transitória e o endereço passa a servir o formulário quando a divulgação chega");

        await ctx.PublicarAsync();
        await ctx.EsperarDivulgacaoAsync();

        HttpResponseMessage servido = await ctx.GetFormularioAsync();

        servido.StatusCode.Should().Be(HttpStatusCode.OK);
        servido.Headers.CacheControl!.NoCache.Should().BeTrue(
            "o endereço também não muda quando uma retificação troca a versão servida");
    }

    private enum Autenticacao
    {
        PlataformaAdmin,
        PapelErrado,
        Nenhuma,
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid DocumentoId)
    {
        /// <summary>
        /// Espera a linha de divulgação aparecer. É ela que torna o certame público, e o formulário
        /// resolve por ela — logo, antes dela, não há formulário a servir.
        /// </summary>
        public async Task EsperarDivulgacaoAsync()
        {
            DateTimeOffset limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTimeOffset.UtcNow < limite)
            {
                await using (AsyncServiceScope scope = Api.Services.CreateAsyncScope())
                {
                    SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
                    if (await db.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == ProcessoId))
                    {
                        return;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300));
            }

            throw new InvalidOperationException(
                $"A divulgação do processo {ProcessoId} não chegou em 30s — sem ela o formulário não é servido.");
        }

        public async Task<Guid> FaseDeInscricaoAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().Include(static p => p.CronogramaFases)
                .SingleAsync(p => p.Id == ProcessoId).ConfigureAwait(false);
            return processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;
        }

        public async Task<HttpResponseMessage> PutFormularioAsync(
            string? titulo,
            Autenticacao autenticar = Autenticacao.PlataformaAdmin,
            string finalidade = "INSCRICAO",
            Guid? faseId = null)
        {
            Guid? fase = faseId ?? (finalidade == "INSCRICAO" ? await FaseDeInscricaoAsync().ConfigureAwait(false) : null);
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/{finalidade}", UriKind.Relative))
            {
                Content = JsonContent.Create(new
                {
                    faseId = fase,
                    titulo,
                    etapas = new object[]
                    {
                        new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, titulo = "Dados" },
                        new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", titulo = "Revisão e aceite" },
                    },
                }),
            };
            Autenticar(request, autenticar);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> GetRascunhoAsync(Autenticacao autenticar, string finalidade = "INSCRICAO")
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/{finalidade}/renderizavel", UriKind.Relative));
            Autenticar(request, autenticar);
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> GetFormularioAsync(string finalidade = "INSCRICAO") => await Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/formularios/{finalidade}", UriKind.Relative)).ConfigureAwait(false);

        public async Task<HttpResponseMessage> PutFatosAsync(IReadOnlyList<object> corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                FormularioDeInscricaoHttp.RotaDosItens(ProcessoId))
            {
                Content = JsonContent.Create(FormularioDeInscricaoHttp.CorpoDosItens(corpo)),
            };
            Autenticar(request, Autenticacao.PlataformaAdmin);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task PublicarAsync()
        {
            using HttpRequestMessage publicar = new(
                HttpMethod.Post,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/publicacao", UriKind.Relative))
            {
                Content = JsonContent.Create(new
                {
                    numero = "001/2026",
                    documentoEditalId = DocumentoId,
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
            Autenticar(publicar, Autenticacao.PlataformaAdmin);
            publicar.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            HttpResponseMessage resposta = await Client.SendAsync(publicar).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, "o cenário depende de um certame publicado");
        }
    }

    private async Task<Contexto> SemearRascunhoAsync(string nome)
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

        return new Contexto(api, client, processoId, documentoId);
    }

    private static void Autenticar(HttpRequestMessage request, Autenticacao autenticacao)
    {
        if (autenticacao == Autenticacao.Nenhuma)
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(
            TestAuthHandler.RolesHeader,
            autenticacao == Autenticacao.PlataformaAdmin ? "plataforma-admin" : "consulta-publica");
    }

    private static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string HojeMais(int dias) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
