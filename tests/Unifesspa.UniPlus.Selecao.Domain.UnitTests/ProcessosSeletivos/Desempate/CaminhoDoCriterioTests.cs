namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Desempate;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;

public sealed class CaminhoDoCriterioTests
{
    [Theory]
    [InlineData(null, "criterios[2]")]
    [InlineData("", "criterios[2].parametros")]
    [InlineData("ordem", "criterios[2].ordem")]
    [InlineData("regraCodigo", "criterios[2].regra.codigo")]
    [InlineData("valor", "criterios[2].parametros.valor")]
    [InlineData("[0].valor", "criterios[2].parametros[0].valor")]
    public void CampoDoCriterioDesempate_CausaRelativa_SituaNoCriterio(string? campo, string esperado)
    {
        FieldError causa = new(campo, new DomainError("codigo", "mensagem"), "origem");

        FieldError resultado = RegrasDoDesempate.CampoDoCriterioDesempate(2, causa);

        resultado.Field.Should().Be(esperado);
        resultado.Origem.Should().Be("origem");
    }
}
