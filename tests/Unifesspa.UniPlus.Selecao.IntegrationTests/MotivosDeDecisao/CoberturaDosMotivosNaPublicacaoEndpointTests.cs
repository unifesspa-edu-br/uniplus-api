namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A conferência de cobertura dos motivos na publicação pelo endpoint real, contra Postgres via Testcontainers
/// (UNI-REQ-0123): com um fundamento de isenção habilitado, a publicação exige, em cada fase ou etapa que declara
/// o tipo de isenção, ao menos um motivo favorável e um desfavorável escolhidos para esse fundamento, e sem fase nem
/// etapa que o declare o fundamento não tem onde ser decidido; o texto publicado é o da escolha (UNI-REQ-0122).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class CoberturaDosMotivosNaPublicacaoEndpointTests : IAsyncLifetime
{
    private readonly CascadingFixture _fixture;
    private readonly ChamadasDosMotivosDeTeste _chamadas;

    public CoberturaDosMotivosNaPublicacaoEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
        _chamadas = new ChamadasDosMotivosDeTeste(fixture, "TEST_CDM");
    }

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao_do_processo WHERE codigo LIKE 'TEST\\_CDM\\_%' ESCAPE '\\'");
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE 'TEST\\_CDM\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "Fundamento de isenção só com motivo desfavorável recusa a publicação citando o fundamento e o sentido favorável")]
    public async Task Publicar_SemMotivoFavoravel_RecusaComCoberturaInsuficiente()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoComIsencaoAsync(nameof(Publicar_SemMotivoFavoravel_RecusaComCoberturaInsuficiente));
        using HttpClient client = _fixture.Factory.CreateClient();
        await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Desfavoravel);

        using HttpResponseMessage recusada = await PublicarAsync(client, processoId, documentoId);

        string corpo = Regex.Unescape(await recusada.Content.ReadAsStringAsync());
        recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.selecao.motivo_de_decisao.cobertura_insuficiente")
            .And.Contain($"motivo favorável para o fundamento de isenção {ProcessoComIsencaoDeTeste.Fundamento}")
            .And.NotContain("motivo desfavorável para o fundamento", "o sentido desfavorável já está coberto");
    }

    [Fact(DisplayName = "Com motivo favorável e desfavorável no fundamento habilitado, a publicação passa")]
    public async Task Publicar_ComOsDoisSentidos_Publica()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoComIsencaoAsync(nameof(Publicar_ComOsDoisSentidos_Publica));
        using HttpClient client = _fixture.Factory.CreateClient();
        await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Desfavoravel);
        await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Favoravel);

        using HttpResponseMessage publicada = await PublicarAsync(client, processoId, documentoId);

        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Fundamento habilitado sem fase nem etapa que declare o tipo de isenção recusa a publicação com o tipo de decisão não declarado")]
    public async Task Publicar_SemLugarQueDeclareOTipo_RecusaComTipoDeDecisaoNaoDeclarado()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoComIsencaoAsync(nameof(Publicar_SemLugarQueDeclareOTipo_RecusaComTipoDeDecisaoNaoDeclarado), tiposDeDecisao: []);
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage recusada = await PublicarAsync(client, processoId, documentoId);

        string corpo = Regex.Unescape(await recusada.Content.ReadAsStringAsync());
        recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.selecao.motivo_de_decisao.tipo_de_decisao_nao_declarado")
            .And.Contain($"O fundamento de isenção {ProcessoComIsencaoDeTeste.Fundamento} é habilitado, mas nenhuma fase nem etapa declara o tipo de decisão");
    }

    [Fact(DisplayName = "A descrição alterada no cadastro depois da escolha não muda o texto que a versão publicada traz")]
    public async Task Publicar_DescricaoAlteradaNoCadastroDepoisDaEscolha_VersaoPublicadaTrazOTextoEscolhido()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoComIsencaoAsync(nameof(Publicar_DescricaoAlteradaNoCadastroDepoisDaEscolha_VersaoPublicadaTrazOTextoEscolhido));
        using HttpClient client = _fixture.Factory.CreateClient();
        MotivoDeDecisao desfavoravel = await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Desfavoravel);
        await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Favoravel);
        const string descricaoNova = "Redação nova do cadastro, posterior à escolha.";
        await AlterarDescricaoNoCadastroAsync(desfavoravel.Id, descricaoNova);

        using HttpResponseMessage publicada = await PublicarAsync(client, processoId, documentoId);

        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
        IReadOnlyList<JsonElement> naVersao1 = await _chamadas.LerAsync(processoId, versao: 1);
        JsonElement publicado = naVersao1.Single(e => e.GetProperty("codigo").GetString() == desfavoravel.Codigo.Valor);
        publicado.GetProperty("descricao").GetString().Should().Be(desfavoravel.Descricao).And.NotBe(descricaoNova);
    }

    private async Task AlterarDescricaoNoCadastroAsync(Guid motivoId, string descricao)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        MotivoDeDecisao motivo = await db.MotivosDeDecisao.SingleAsync(m => m.Id == motivoId);
        motivo.Atualizar(descricao).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoComIsencaoAsync(string nome, string[]? tiposDeDecisao = null) =>
        ProcessoComIsencaoDeTeste.SemearAsync(_fixture.Factory.Services, nome, tiposDeDecisao);

    private async Task<MotivoDeDecisao> IncluirNovoMotivoAsync(HttpClient client, Guid processoId, SentidoDoMotivo sentido)
    {
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoDeIsencaoAsync(sentido);
        Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(_fixture.Factory.Services, processoId, ProcessoComIsencaoDeTeste.CodigoDaFase);

        using HttpResponseMessage inclusao = await EnviarAsync(
            client, HttpMethod.Put, LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, motivo.Id), new { justificativa = (string?)null });
        inclusao.StatusCode.Should().Be(HttpStatusCode.NoContent, await inclusao.Content.ReadAsStringAsync());

        return motivo;
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoId) =>
        EnviarAsync(client, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao", new
        {
            numero = (string?)null,
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
        });

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object corpo)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
