namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O histórico das versões do termo, paginado pelo número da versão (ADR-0162): as páginas seguem a ordem dos números,
/// mesmo quando os ids estão fora dela, sem repetir nem pular versão, e o cursor com chave de ordenação que não é número
/// é recusado.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class HistoricoDoTermoNoHostTests : TermoNoHostTestBase
{
    private const string RotaDoHistorico = RotaDoTermo + "/versoes";

    public HistoricoDoTermoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O histórico pagina pelo número da versão, sem repetir nem pular, mesmo com os ids fora da ordem dos números")]
    public async Task Historico_PaginaPeloNumero()
    {
        await TermoNoHostDeTeste.PublicarAsync(Fixture.Factory.Services, "segunda versão");
        await TermoNoHostDeTeste.PublicarAsync(Fixture.Factory.Services, "terceira versão");
        await EmbaralharOsIdsAsync();
        using HttpClient diogo = await PublicadorAsync();

        int[][] paginas = await PaginasAsync(diogo, $"{RotaDoHistorico}?limit=2");

        paginas.Should().HaveCount(2);
        paginas.SelectMany(static p => p).Should().Equal(1, 2, 3);
    }

    [Theory(DisplayName = "O cursor com chave de ordenação que não é número de versão é recusado com 400")]
    [InlineData("não numérica")]
    [InlineData("-1")]
    public async Task Historico_CursorAdulterado_Recusa(string chave)
    {
        using HttpClient diogo = await PublicadorAsync();

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{RotaDoHistorico}?cursor={Uri.EscapeDataString(await CursorAsync(chave))}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest, await resposta.Content.ReadAsStringAsync());
    }

    // A versão 1 fica com o maior id possível e a 2 com o menor: a ordem pelo id seria 2, 3, 1. Nenhuma das duas é a
    // vigente nem está fixada em pedido, então trocar o id não fere chave estrangeira.
    private Task EmbaralharOsIdsAsync() =>
        ExecutarSqlAsync(
            "UPDATE autorizacao.versoes_do_termo SET id = CASE numero WHEN 1 THEN {0} ELSE {1} END WHERE numero IN (1, 2)",
            Guid.Parse("ffffffff-ffff-7fff-bfff-ffffffffffff"),
            Guid.Parse("00000000-0000-7000-8000-000000000001"));

    private async Task<string> CursorAsync(string chave)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<CursorEncoder>().EncodeAsync(new CursorPayload(
            After: Guid.CreateVersion7().ToString(),
            Limit: 2,
            ResourceTag: "versoes-do-termo",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            Direction: PaginationDirection.Next,
            SortKey: chave));
    }

    // As páginas da listagem, uma a uma pelo cursor do cabeçalho Link, com os números das versões de cada uma.
    private static async Task<int[][]> PaginasAsync(HttpClient cliente, string primeira)
    {
        List<int[]> paginas = [];
        for (Uri? pagina = new(primeira, UriKind.Relative); pagina is not null;)
        {
            using HttpResponseMessage resposta = await cliente.GetAsync(pagina);
            JsonElement itens = await CorpoAsync(resposta, HttpStatusCode.OK);
            paginas.Add([.. itens.EnumerateArray().Select(static i => i.GetProperty("numero").GetInt32())]);
            pagina = resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links) ? Proxima(links) : null;
        }

        return [.. paginas];
    }

    private static Uri? Proxima(IEnumerable<string> links) =>
        links.SelectMany(static l => l.Split(',')).Select(static l => l.Trim())
            .Where(static l => l.EndsWith("rel=\"next\"", StringComparison.Ordinal))
            .Select(static l => new Uri(l[(l.IndexOf('<', StringComparison.Ordinal) + 1)..l.IndexOf('>', StringComparison.Ordinal)], UriKind.RelativeOrAbsolute))
            .FirstOrDefault();
}
