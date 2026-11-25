namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class OrigemDaBaseCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<OrigemDaBase>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<OrigemDaBase, string>
            {
                [OrigemDaBase.Migracao] = "MIGRACAO",
                [OrigemDaBase.Saida] = "SAIDA",
                [OrigemDaBase.Liberacao] = "LIBERACAO",
                [OrigemDaBase.Conciliacao] = "CONCILIACAO",
            });
}
