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
          "regraCodigo": "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "regraVersao": "v1",
          "prazoValor": 2, "prazoUnidade": "DIAS_UTEIS", "atoAncoraCodigo": "RESULTADO_PRELIMINAR",
          "efeitoSuspensivo": "SEM_EFEITO_SUSPENSIVO"
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
          "ancora": "PUBLICACAO_DO_ATO", "regraCodigo": "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "regraVersao": "v1",
          "prazoValor": 2, "prazoUnidade": "DIAS_UTEIS", "atoAncoraCodigo": "RESULTADO_PRELIMINAR",
          "efeitoSuspensivo": "SEM_EFEITO_SUSPENSIVO"
        }
        """;

        Action act = () => JsonSerializer.Deserialize<RecursoDaEtapaInput>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Com a declaração de instância superior, a regra de recurso da fase é lida com os códigos como texto")]
    public void RegraDaFase_ComDeclaracao_LeOsCodigosComoTexto()
    {
        const string json = """
        {
          "regraCodigo": "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "regraVersao": "v1",
          "prazoValor": 2, "prazoUnidade": "DIAS_UTEIS", "atoAncoraCodigo": "RESULTADO_PRELIMINAR",
          "admiteInstanciaSuperior": true, "prazoSegundaInstanciaValor": 3, "prazoSegundaInstanciaUnidade": "HORAS",
          "efeitoSuspensivo": "SEM_EFEITO_SUSPENSIVO", "guardaDaVaga": "NAO_GUARDA"
        }
        """;

        RegraRecursoFaseInput? regra = JsonSerializer.Deserialize<RegraRecursoFaseInput>(json, Options);

        regra.Should().NotBeNull();
        (regra!.PrazoUnidade, regra.PrazoSegundaInstanciaUnidade, regra.EfeitoSuspensivo, regra.GuardaDaVaga)
            .Should().Be(("DIAS_UTEIS", "HORAS", "SEM_EFEITO_SUSPENSIVO", "NAO_GUARDA"));
    }
}
