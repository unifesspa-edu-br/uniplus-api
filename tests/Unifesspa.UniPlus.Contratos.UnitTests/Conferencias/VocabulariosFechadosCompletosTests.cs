namespace Unifesspa.UniPlus.Contratos.UnitTests;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class VocabulariosFechadosCompletosTests
{
    [Theory(DisplayName = "O OpenAPI declara os vocabulários aceitos pelo domínio")]
    [InlineData("DefinirTaxaInscricaoRequest", "fundamentos")]
    [InlineData("DefinirConfiguracaoDivulgacaoRequest", "camposPublicos")]
    public async Task Request_VocabularioPublicado_CoincideComODominio(string esquema, string propriedade)
    {
        HttpClient client = HostsDoProdutoIniciados.Obter(HostsDoProdutoIniciados.UniPlus).Cliente;
        using JsonDocument documento = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/selecao.json", UriKind.Relative)));
        JsonElement valores = documento.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(esquema).GetProperty("properties").GetProperty(propriedade).GetProperty("items").GetProperty("enum");
        string?[] publicados = [.. valores.EnumerateArray().Select(static valor => valor.GetString())];
        IEnumerable<string> esperados = propriedade == "fundamentos"
            ? FundamentoIsencaoCodigo.Descritos.Select(static f => f.Codigo)
            : ConfiguracaoDivulgacao.CamposPermitidos.Select(static c => c.Codigo);

        publicados.Should().BeEquivalentTo(esperados);
    }
}
