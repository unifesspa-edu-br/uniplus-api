namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// As propriedades do recurso que o esquema retirou (os valores e as unidades de suspensividade da 1ª e da 2ª instância) não valem
/// mais em nenhum documento: ficam recusadas como propriedade desconhecida, com o caminho da propriedade, na fase e na etapa.
/// </summary>
public sealed class PrazosDoRecursoDoEsquemaDaConfiguracaoTests
{
    private static JsonNode DocumentoComPropriedadeRetirada(bool naFase, string propriedade)
    {
        JsonNode documento = Carregar(Path.Join("exemplos", "inscricao-pelo-sistema.json"));
        JsonNode recurso = naFase
            ? documento["cronograma"]!["fases"]![0]!["regraRecurso"] = Documento("""{ "regraCodigo": "RECURSO-PRAZO", "regraVersao": "v1" }""")
            : documento["etapas"]![0]!["recursos"]![0]!;
        recurso[propriedade] = 2;
        return documento;
    }

    [Theory(DisplayName = "O documento com a propriedade de suspensividade retirada é recusado, com o caminho da propriedade")]
    [InlineData(true, "suspensividadePrimeiraInstanciaValor", "/cronograma/fases/0/regraRecurso/suspensividadePrimeiraInstanciaValor")]
    [InlineData(true, "suspensividadePrimeiraInstanciaUnidade", "/cronograma/fases/0/regraRecurso/suspensividadePrimeiraInstanciaUnidade")]
    [InlineData(true, "suspensividadeSegundaInstanciaValor", "/cronograma/fases/0/regraRecurso/suspensividadeSegundaInstanciaValor")]
    [InlineData(true, "suspensividadeSegundaInstanciaUnidade", "/cronograma/fases/0/regraRecurso/suspensividadeSegundaInstanciaUnidade")]
    [InlineData(false, "suspensividadePrimeiraInstanciaValor", "/etapas/0/recursos/0/suspensividadePrimeiraInstanciaValor")]
    [InlineData(false, "suspensividadePrimeiraInstanciaUnidade", "/etapas/0/recursos/0/suspensividadePrimeiraInstanciaUnidade")]
    [InlineData(false, "suspensividadeSegundaInstanciaValor", "/etapas/0/recursos/0/suspensividadeSegundaInstanciaValor")]
    [InlineData(false, "suspensividadeSegundaInstanciaUnidade", "/etapas/0/recursos/0/suspensividadeSegundaInstanciaUnidade")]
    public void PropriedadeDeSuspensividadeRetirada_Recusa(bool naFase, string propriedade, string caminhoEsperado)
    {
        ArgumentNullException.ThrowIfNull(propriedade);
        ArgumentNullException.ThrowIfNull(caminhoEsperado);

        CaminhosComErro(DocumentoComPropriedadeRetirada(naFase, propriedade)).Should().Contain(caminhoEsperado);
    }

    [Theory(DisplayName = "O mesmo documento sem a propriedade retirada é válido, o que prova que o caminho recusado é o da propriedade")]
    [InlineData(true)]
    [InlineData(false)]
    public void SemAPropriedadeRetirada_Valida(bool naFase)
    {
        JsonNode documento = DocumentoComPropriedadeRetirada(naFase, "prazoValor");

        CaminhosComErro(documento).Should().BeEmpty();
    }
}
