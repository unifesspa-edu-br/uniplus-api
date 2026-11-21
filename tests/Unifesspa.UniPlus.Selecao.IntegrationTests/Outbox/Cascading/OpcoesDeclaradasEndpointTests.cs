namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// As opções que o processo declara para um fato de fonte do processo, como as edições do ENEM
/// admitidas, voltam na leitura do processo, mesmo que nenhum formulário colete o fato ainda.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class OpcoesDeclaradasEndpointTests
{
    private readonly CascadingFixture _fixture;

    public OpcoesDeclaradasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "GET /processos-seletivos/{id} devolve as edições do ENEM declaradas, na ordem, sem o formulário coletá-las")]
    public async Task Obter_EdicoesDeclaradas_VoltamNaLeitura()
    {
        Guid processoId = await SemearProcessoDoEnemAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        await DefinaFormularioSemItensAsync(client, processoId);
        await DeclareAsEdicoesDoEnemAsync(client, processoId);
        using JsonDocument processo = await LeiaOProcessoAsync(client, processoId);

        ConfiraQueNenhumFormularioColetaAsEdicoes(processo);
        ConfiraAsEdicoesDeclaradasNaOrdem(processo);
    }

    private static async Task DefinaFormularioSemItensAsync(HttpClient client, Guid processoId) =>
        await PutSemConteudoAsync(client, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens",
            new { itens = Array.Empty<object>(), grupos = Array.Empty<object>() });

    private static async Task DeclareAsEdicoesDoEnemAsync(HttpClient client, Guid processoId) =>
        await PutSemConteudoAsync(client, $"/api/selecao/processos-seletivos/{processoId}/fatos/EDICAO_ENEM/opcoes",
            new { opcoes = new[] { new { codigo = "2026", rotulo = "ENEM 2026" }, new { codigo = "2025", rotulo = "ENEM 2025" } } });

    private static async Task PutSemConteudoAsync(HttpClient client, string rota, object corpo)
    {
        using HttpRequestMessage request = Requisicao(HttpMethod.Put, rota);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", ApoioDaPublicacaoDoProcessoSeletivo.MakeIdempotencyKey());
        request.Content = JsonContent.Create(corpo);
        HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<JsonDocument> LeiaOProcessoAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        request.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static void ConfiraQueNenhumFormularioColetaAsEdicoes(JsonDocument processo) =>
        processo.RootElement.GetProperty("formularios").EnumerateArray()
            .SelectMany(static f => FormularioNaRespostaDoProcesso.Itens(f))
            .Select(static f => f.GetProperty("fatoCodigo").GetString())
            .Should().NotContain("EDICAO_ENEM");

    private static void ConfiraAsEdicoesDeclaradasNaOrdem(JsonDocument processo)
    {
        JsonElement edicoes = processo.RootElement.GetProperty("opcoesDeclaradas").EnumerateArray()
            .Single(static o => o.GetProperty("fatoCodigo").GetString() == "EDICAO_ENEM");
        edicoes.GetProperty("opcoes").EnumerateArray()
            .Select(static o => (o.GetProperty("codigo").GetString(), o.GetProperty("rotulo").GetString(), o.GetProperty("ordem").GetInt32()))
            .Should().Equal(("2026", "ENEM 2026", 0), ("2025", "ENEM 2025", 1));
    }

    private async Task<Guid> SemearProcessoDoEnemAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoEnemComCalculoLocalSeeder.SemearAsync(
            db, $"Opções declaradas {Guid.CreateVersion7()}", ofertaComGrupo: true);
        return processo.Id;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string rota)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        ApoioDaPublicacaoDoProcessoSeletivo.AppendTestAuth(request);
        return request;
    }
}
