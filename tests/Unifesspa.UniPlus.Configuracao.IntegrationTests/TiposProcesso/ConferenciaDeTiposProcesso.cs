namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

internal static class ConferenciaDeTiposProcesso
{
    internal static async Task ConferirTipoNaListagemPublica(HttpClient client, Guid id, string codigo)
    {
        HttpResponseMessage listar = await client.GetAsync(new Uri("/api/configuracao/tipos-processo", UriKind.Relative));
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id)
            .GetProperty("codigo").GetString().Should().Be(codigo);
    }

    internal static async Task ConferirTipoReativado(HttpClient client, Guid id, string codigo)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument item = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("codigo").GetString().Should().Be(codigo, "reativar não é caminho lateral para trocar a identidade");
        item.RootElement.GetProperty("nome").GetString().Should().Be("Seleção reativável");
        item.RootElement.GetProperty("descricao").GetString().Should().Be("Descrição preservada");
        item.RootElement.GetProperty("ativo").GetBoolean().Should().BeTrue();
    }

}
