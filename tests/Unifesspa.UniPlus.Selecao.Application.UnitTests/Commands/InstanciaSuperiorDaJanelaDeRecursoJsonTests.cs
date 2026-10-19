namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// A declaração de instância superior é obrigatória na entrada: <c>bool</c> ausente
/// desserializa como <c>false</c>, e a janela seria congelada afirmando que o recurso termina
/// na 1ª instância sem que ninguém o tivesse decidido.
/// </summary>
public sealed class InstanciaSuperiorDaJanelaDeRecursoJsonTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact(DisplayName = "Regra de recurso da fase sem a declaração de instância superior é rejeitada, e não lida como 'não admite'")]
    public void RegraDaFase_SemDeclaracao_LancaJsonException()
    {
        const string json = """
        {
          "regraCodigo": "RECURSO-PRAZO-ANCORADO-EM-ATO", "regraVersao": "v1",
          "prazoValor": 2, "prazoUnidade": "DiasUteis", "atoAncoraCodigo": "RESULTADO_PRELIMINAR",
          "suspensividadePrimeiraInstanciaValor": null, "suspensividadePrimeiraInstanciaUnidade": null,
          "suspensividadeSegundaInstanciaValor": null, "suspensividadeSegundaInstanciaUnidade": null
        }
        """;

        Action act = () => JsonSerializer.Deserialize<RegraRecursoFaseInput>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Janela de recurso da etapa sem a declaração de instância superior é rejeitada, e não lida como 'não admite'")]
    public void JanelaDaEtapa_SemDeclaracao_LancaJsonException()
    {
        const string json = """
        {
          "ancora": "AtoPublicado", "regraCodigo": "RECURSO-PRAZO-ANCORADO-EM-ATO", "regraVersao": "v1",
          "prazoValor": 2, "prazoUnidade": "DiasUteis", "atoAncoraCodigo": "RESULTADO_PRELIMINAR",
          "suspensividadePrimeiraInstanciaValor": null, "suspensividadePrimeiraInstanciaUnidade": null,
          "suspensividadeSegundaInstanciaValor": null, "suspensividadeSegundaInstanciaUnidade": null
        }
        """;

        Action act = () => JsonSerializer.Deserialize<RecursoDaEtapaInput>(json, Options);

        act.Should().Throw<JsonException>();
    }
}
