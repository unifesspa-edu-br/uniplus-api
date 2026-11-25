namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class DesfechoDaTrocaDeVersaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<DesfechoDaTrocaDeVersao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<DesfechoDaTrocaDeVersao, string>
            {
                [DesfechoDaTrocaDeVersao.Inalterado] = "INALTERADO",
                [DesfechoDaTrocaDeVersao.VersaoAtualizada] = "VERSAO_ATUALIZADA",
                [DesfechoDaTrocaDeVersao.VoltouAoGestor] = "VOLTOU_AO_GESTOR",
                [DesfechoDaTrocaDeVersao.Devolvido] = "DEVOLVIDO",
            });
}
