namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;

/// <summary>
/// As leituras que outra requisição faz do certame enquanto a retificação acontece: a versão que vigora num instante, a
/// página pública do certame divulgado e a lista de atos do processo.
/// </summary>
internal static class LeiturasDoCertameNaRetificacao
{
    public static async Task<int> NumeroDaVersaoVigenteAsync(IServiceProvider servicos, Guid processoId, DateTimeOffset instante)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        Result<ConfiguracaoVigente> vigente = await escopo.ServiceProvider.GetRequiredService<ConfiguracaoVigenteDoProcesso>()
            .LerAsync(processoId, instante, CancellationToken.None);
        vigente.IsSuccess.Should().BeTrue(vigente.Error?.Message);

        return vigente.Value!.NumeroDaVersao;
    }

    /// <summary>O documento do edital que a página pública do certame divulga.</summary>
    public static async Task<Guid> DocumentoDivulgadoAsync(HttpClient cliente, Guid processoId)
    {
        using HttpResponseMessage resposta = await cliente.GetAsync(
            new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative), CancellationToken.None);
        resposta.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using JsonDocument pagina = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return pagina.RootElement.GetProperty("documentoEdital").GetProperty("documentoEditalId").GetGuid();
    }

    public static async Task<IReadOnlyList<Guid>> AtosListadosAsync(HttpClient cliente, Guid processoId) =>
        [.. (await PublicarAtoAvulsoEndpointTests.ConsultarAtosAsync(cliente, processoId)).Select(static a => a.GetProperty("id").GetGuid())];
}
