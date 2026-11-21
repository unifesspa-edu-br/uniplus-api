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

        HttpResponseMessage semOCampo = await ProcessoSeletivoHttp.DefinirItensAsync(client, processoId, new { itens = Array.Empty<object>(), grupos = Array.Empty<object>() });
        semOCampo.StatusCode.Should().Be(HttpStatusCode.NoContent, await semOCampo.Content.ReadAsStringAsync());
        HttpResponseMessage declaradas = await DeclararEdicoesDoEnemAsync(client, processoId);
        declaradas.StatusCode.Should().Be(HttpStatusCode.NoContent, await declaradas.Content.ReadAsStringAsync());

        using JsonDocument corpo = await ProcessoSeletivoHttp.ObterAsync(client, processoId);
        corpo.RootElement.GetProperty("formularios").EnumerateArray().SelectMany(static f => FormularioNaRespostaDoProcesso.Itens(f))
            .Select(static f => f.GetProperty("fatoCodigo").GetString())
            .Should().NotContain("EDICAO_ENEM");
        ConferirEdicoesDeclaradas(corpo.RootElement);
    }

    private static async Task<HttpResponseMessage> DeclararEdicoesDoEnemAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage opcoes = ProcessoSeletivoHttp.Requisicao(HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/fatos/EDICAO_ENEM/opcoes");
        opcoes.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        opcoes.Content = JsonContent.Create(new
        {
            opcoes = new[] { new { codigo = "2026", rotulo = "ENEM 2026" }, new { codigo = "2025", rotulo = "ENEM 2025" } },
        });
        return await client.SendAsync(opcoes);
    }

    private static void ConferirEdicoesDeclaradas(JsonElement processo)
    {
        JsonElement edicoes = processo.GetProperty("opcoesDeclaradas").EnumerateArray()
            .Single(static o => o.GetProperty("fatoCodigo").GetString() == "EDICAO_ENEM");
        edicoes.GetProperty("opcoes").EnumerateArray()
            .Select(static o => (o.GetProperty("codigo").GetString(), o.GetProperty("rotulo").GetString(), o.GetProperty("ordem").GetInt32()))
            .Should().Equal(("2026", "ENEM 2026", 0), ("2025", "ENEM 2025", 1));
    }

    private async Task<Guid> SemearProcessoDoEnemAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPendenciasSeeder.SemearEnemComCalculoLocalAsync(
            db, $"Opções declaradas {Guid.CreateVersion7()}", ofertaComGrupo: true);
        return processo.Id;
    }
}
