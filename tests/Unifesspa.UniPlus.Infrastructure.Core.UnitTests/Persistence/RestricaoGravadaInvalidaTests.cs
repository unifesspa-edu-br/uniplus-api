namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Serializacao;

public sealed class RestricaoGravadaInvalidaTests
{
    [Fact(DisplayName = "Faixa de data gravada com os dois limites inválidos faz a reidratação lançar com os dois códigos distintos")]
    public void Reidratar_FaixaDeDataComOsDoisLimitesInvalidos_LancaComOsCodigos()
    {
        using JsonDocument gravado = JsonDocument.Parse(
            """[{"tipo":"FAIXA_DE_DATA","dataMinima":{"referenciaDaData":"HOJE"},"dataMaxima":{"referenciaDaData":"DATA_FIXA","data":"10/01/2027"}}]""");

        Action reidratar = () => ObjetoDeValorGravado.Reidratar(RestricaoValorJson.ListaDeJson(gravado.RootElement), "restrições");

        reidratar.Should().Throw<InvalidOperationException>().WithMessage(
            $"*{RestricaoValorErrorCodes.LimiteDeDataInvalido}, {RestricaoValorErrorCodes.FormaJsonInvalida}*");
    }
}
