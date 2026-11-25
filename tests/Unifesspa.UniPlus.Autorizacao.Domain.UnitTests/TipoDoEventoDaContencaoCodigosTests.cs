namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoDoEventoDaContencaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDoEventoDaContencao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<TipoDoEventoDaContencao, string>
            {
                [TipoDoEventoDaContencao.Suspensao] = "SUSPENSAO",
                [TipoDoEventoDaContencao.Retomada] = "RETOMADA",
                [TipoDoEventoDaContencao.Desativacao] = "DESATIVACAO",
                [TipoDoEventoDaContencao.Reativacao] = "REATIVACAO",
            });
}
