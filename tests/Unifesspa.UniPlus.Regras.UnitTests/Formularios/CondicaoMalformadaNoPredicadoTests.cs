namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class CondicaoMalformadaNoPredicadoTests
{
    [Theory]
    [InlineData("[[{}]]", "[0][0].codigoDoDado")]
    [InlineData("[[{\"codigoDoDado\":\"X\"}]]", "[0][0].operador")]
    [InlineData("[[{\"codigoDoDado\":\"X\",\"operador\":\"IGUAL\"}]]", "[0][0].valor")]
    [InlineData("[[null]]", "[0][0]")]
    public void DeJson_CondicaoMalformada_ApontaCampoEMensagem(string json, string campo)
    {
        using JsonDocument documento = JsonDocument.Parse(json);

        Result<PredicadoDnf> resultado = PredicadoDnfJson.DeJson(documento.RootElement);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            (campo, PredicadoDnfJson.FormaInvalida, "Cada condição tem codigoDoDado, operador e valor."));
    }
}
