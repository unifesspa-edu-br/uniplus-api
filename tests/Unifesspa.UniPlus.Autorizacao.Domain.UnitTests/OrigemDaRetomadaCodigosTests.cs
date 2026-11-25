namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class OrigemDaRetomadaCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<OrigemDaRetomada>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<OrigemDaRetomada, string>
            {
                [OrigemDaRetomada.DecisaoDaEtapa] = "DECISAO_DA_ETAPA",
                [OrigemDaRetomada.TrocaDeVigencia] = "TROCA_DE_VIGENCIA",
                [OrigemDaRetomada.Reclassificacao] = "RECLASSIFICACAO",
            });
}
