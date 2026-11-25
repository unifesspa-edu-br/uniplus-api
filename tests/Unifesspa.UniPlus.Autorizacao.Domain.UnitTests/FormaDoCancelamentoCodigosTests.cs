namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class FormaDoCancelamentoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<FormaDoCancelamento>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<FormaDoCancelamento, string>
            {
                [FormaDoCancelamento.Pessoa] = "PESSOA",
                [FormaDoCancelamento.Contencao] = "CONTENCAO",
            });
}
