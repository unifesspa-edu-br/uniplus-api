namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// A declaração de instância superior é obrigatória na entrada: ausente ou nula, chega ao comando como nula, e o domínio a
/// recusa. Ela não pode ser lida como <c>false</c>, senão a janela seria congelada afirmando que o recurso termina na 1ª
/// instância sem que ninguém o tivesse decidido.
/// </summary>
public sealed class InstanciaSuperiorDaJanelaDeRecursoJsonTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact(DisplayName = "Regra de recurso da fase sem a declaração de instância superior é lida como nula, e não como 'não admite'")]
    public void RegraDaFase_SemDeclaracao_LeComoNula()
    {
        const string json = """
        {
          "regraCodigo": "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "regraVersao": "v1",
          "prazoValor": 2, "prazoUnidade": "DIAS_UTEIS", "atoAncoraCodigo": "RESULTADO_PRELIMINAR",
          "efeitoSuspensivo": "SEM_EFEITO_SUSPENSIVO"
        }
        """;

        RegraRecursoFaseInput? regra = JsonSerializer.Deserialize<RegraRecursoFaseInput>(json, Options);

        regra!.AdmiteInstanciaSuperior.Should().BeNull();
    }

    [Fact(DisplayName = "Janela de recurso da etapa sem a declaração de instância superior é lida como nula, e não como 'não admite'")]
    public void JanelaDaEtapa_SemDeclaracao_LeComoNula()
    {
        const string json = """
        {
          "ancora": "PUBLICACAO_DO_ATO", "regraCodigo": "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "regraVersao": "v1",
          "prazoValor": 2, "prazoUnidade": "DIAS_UTEIS", "atoAncoraCodigo": "RESULTADO_PRELIMINAR",
          "efeitoSuspensivo": "SEM_EFEITO_SUSPENSIVO"
        }
        """;

        RecursoDaEtapaInput? janela = JsonSerializer.Deserialize<RecursoDaEtapaInput>(json, Options);

        janela!.AdmiteInstanciaSuperior.Should().BeNull();
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
