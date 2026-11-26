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
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O endpoint <c>PUT /admin/processos-seletivos/{id}/formularios/{codigo}/itens</c>, fim a fim
/// pelo HTTP. Prova o que só o ciclo real prova: a coletabilidade e o vocabulário resolvidos
/// contra o seed cross-módulo de Configuração, o guard de rascunho num processo publicado, a
/// idempotência do replay e a autorização herdada do controller.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirFatosColetadosEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirFatosColetadosEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Coleta válida em rascunho é aceita com 204 sem ETag e persiste os fatos e as restrições de valor")]
    public async Task Rascunho_ColetaValida_204SemEtag()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_ColetaValida_204SemEtag));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(ItemQueCitaUmDadoBasicoComoAnterior());

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        List<FatoColetado> fatos = await FatosPersistidosAsync(ctx);
        fatos.Select(f => f.FatoCodigo).Should().BeEquivalentTo([.. ConjuntoBasicoDaInscricao.Fatos, "BAIXA_RENDA"]);
        fatos.Single(f => f.FatoCodigo == "NATURALIDADE_MUNICIPIO").Restricoes.Should().ContainSingle()
            .Which.Should().BeOfType<MunicipiosDaUf>().Which.FatoUf.Should().Be("NATURALIDADE_UF");
    }

    /// <summary>
    /// O conjunto básico entra sem ser enviado, com as restrições dele; o item do cliente cita um dado básico
    /// como anterior.
    /// </summary>
    private static object[] ItemQueCitaUmDadoBasicoComoAnterior() =>
    [
        FormularioDeInscricaoHttp.Item(
            "BAIXA_RENDA", 0, "Baixa renda", "SIM_OU_NAO", "NUNCA",
            exibicao: new[] { new[] { new { fato = "COR_RACA", operador = "IGUAL", valor = "PRETA" } } }),
    ];

    private static async Task<List<FatoColetado>> FatosPersistidosAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Set<FatoColetado>().AsNoTracking().Where(f => f.ProcessoSeletivoId == ctx.ProcessoId).ToListAsync();
    }

    [Fact(DisplayName = "Fato fora do vocabulário é recusado com 422 sem tradução")]
    public async Task Rascunho_FatoDesconhecido_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_FatoDesconhecido_422));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [FormularioDeInscricaoHttp.Item("V", 0, "V", "ESCOLHA_UNICA", "NUNCA")]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "A recusa do validator aponta o campo pelo nome do corpo, itens[i]")]
    public async Task Rascunho_PrecondicaoVazia_FieldPeloCorpo()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_PrecondicaoVazia_FieldPeloCorpo));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [FormularioDeInscricaoHttp.Item("COR_RACA", 0, "Cor ou raça", "ESCOLHA_UNICA", "NUNCA", exibicao: Array.Empty<object>())]);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())
            .Should().Equal("itens[0].regras.exibicao");
    }

    [Fact(DisplayName = "Coletar um fato derivado (MODALIDADE) é recusado com 422")]
    public async Task Rascunho_FatoDerivado_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_FatoDerivado_422));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [FormularioDeInscricaoHttp.Item("MODALIDADE", 0, "Modalidade", "ESCOLHA_MULTIPLA", "NUNCA")]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Redefinir a coleta de um processo publicado é recusado com 422 (só em rascunho)")]
    public async Task Publicado_Recusa_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Publicado_Recusa_422));
        await ctx.PublicarAsync();

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [FormularioDeInscricaoHttp.Item("COR_RACA", 0, "Cor ou raça", "ESCOLHA_UNICA", "NUNCA")]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Replay com a mesma Idempotency-Key e o mesmo corpo devolve o mesmo 204")]
    public async Task Rascunho_ReplayIdempotente_204()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Rascunho_ReplayIdempotente_204));
        object[] corpo = [FormularioDeInscricaoHttp.Item("BAIXA_RENDA", 0, "Baixa renda", "SIM_OU_NAO", "NUNCA")];
        string chave = MakeIdempotencyKey();

        (await ctx.PutFatosAsync(corpo, idempotencyKey: chave)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage replay = await ctx.PutFatosAsync(corpo, idempotencyKey: chave);

        replay.StatusCode.Should().Be(HttpStatusCode.NoContent);
        replay.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
    }

    [Fact(DisplayName = "Sem autenticação é 401")]
    public async Task SemAutenticacao_401()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemAutenticacao_401));

        HttpResponseMessage resposta = await ctx.PutFatosAsync(
            [FormularioDeInscricaoHttp.Item("COR_RACA", 0, "Cor ou raça", "ESCOLHA_UNICA", "NUNCA")], autenticar: Autenticacao.Nenhuma);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private enum Autenticacao
    {
        PlataformaAdmin,
        Nenhuma,
    }

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid DocumentoId)
    {
        public async Task<HttpResponseMessage> PutFatosAsync(
            IReadOnlyList<object> corpo, string? idempotencyKey = null, Autenticacao autenticar = Autenticacao.PlataformaAdmin)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                FormularioDeInscricaoHttp.RotaDosItens(ProcessoId))
            {
                Content = JsonContent.Create(FormularioDeInscricaoHttp.CorpoDosItens(corpo)),
            };
            Autenticar(request, autenticar);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        private object CorpoDaPublicacao() => new
        {
            numero = "001/2026",
            documentoEditalId = DocumentoId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataDoAto = Hoje(),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = "EDITAL_ABERTURA",
            },
        };

        public async Task PublicarAsync()
        {
            using HttpRequestMessage publicar = new(
                HttpMethod.Post,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/publicacao", UriKind.Relative))
            {
                Content = JsonContent.Create(CorpoDaPublicacao()),
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

        (Guid processoId, Guid documentoId) = await SemearProcessoPublicavelAsync(api, nome);

        return new Contexto(api, client, processoId, documentoId);
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoPublicavelAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}" });

        return (processo.Id, documento.Id);
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
            "plataforma-admin");
    }

    private static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string HojeMais(int dias) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
