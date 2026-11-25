namespace Unifesspa.UniPlus.Contratos.UnitTests;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class VocabulariosFechadosCompletosTests
{
    [Fact(DisplayName = "O OpenAPI declara os campos públicos aceitos pelo domínio")]
    public async Task Request_VocabularioPublicado_CoincideComODominio()
    {
        HttpClient client = HostsDoProdutoIniciados.Obter(HostsDoProdutoIniciados.UniPlus).Cliente;
        using JsonDocument documento = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/selecao.json", UriKind.Relative)));
        JsonElement valores = documento.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("DefinirConfiguracaoDivulgacaoRequest").GetProperty("properties").GetProperty("camposPublicos").GetProperty("items").GetProperty("enum");
        string?[] publicados = [.. valores.EnumerateArray().Select(static valor => valor.GetString())];
        IEnumerable<string> esperados = ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo);

        publicados.Should().BeEquivalentTo(esperados);
    }
}
