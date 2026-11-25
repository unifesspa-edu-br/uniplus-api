namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.RequisicoesDeTipoProcesso;

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

    internal static async Task AtualizarEConferirTipo(HttpClient client, Guid id, string codigo)
    {
        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, new
        {
            id,
            nome = "Seleção renomeada",
            descricao = "Descrição atualizada",
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));
        using JsonDocument item = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("codigo").GetString().Should().Be(codigo, "o código é a identidade imutável do tipo de processo");
        item.RootElement.GetProperty("nome").GetString().Should().Be("Seleção renomeada");
        item.RootElement.TryGetProperty("_links", out _).Should().BeTrue("a leitura pública expõe HATEOAS nível 1");
    }

    internal static async Task<string> LerLinkProprioDoTipo(HttpResponseMessage listagem, Guid id)
    {
        using JsonDocument lista = JsonDocument.Parse(await listagem.Content.ReadAsStringAsync());
        return lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id)
            .GetProperty("_links").GetProperty("self").GetString()!;
    }

    internal static Guid[] IdentificarTiposLegados(JsonDocument lista)
    {
        HashSet<string> codigosLegados = new(StringComparer.Ordinal)
        {
            "SISU", "INDIGENA_E_QUILOMBOLA", "EDUCACAO_DO_CAMPO", "VAGAS_REMANESCENTES", "TRANSFERENCIA_INTERNA",
            "TRANSFERENCIA_EXTERNA", "PORTADOR_DE_DIPLOMA", "REOPCAO",
        };
        return lista.RootElement.EnumerateArray()
            .Where(item => codigosLegados.Contains(item.GetProperty("codigo").GetString()!))
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
    }

    internal static async Task<Guid[]> LerIdsAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. lista.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }
    internal static bool EhUuidV7Rfc9562(Guid id)
    {
        string representacao = id.ToString("D");
        return id.Version == 7 && representacao[19] is '8' or '9' or 'a' or 'b';
    }

    internal static async Task ConferirViolacoesDeCodigoENome(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_processo.codigo_obrigatorio");
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("codigo");
        errors[1].GetProperty("field").GetString().Should().Be("nome");
    }

}
