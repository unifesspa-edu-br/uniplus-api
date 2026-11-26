namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoDoRegistroDoBloqueioCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDoRegistroDoBloqueio>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(Esperados);

    private static readonly Dictionary<TipoDoRegistroDoBloqueio, string> Esperados = new()
    {
        [TipoDoRegistroDoBloqueio.Suspensao] = "SUSPENSAO",
        [TipoDoRegistroDoBloqueio.DecisaoDaRetomada] = "DECISAO_DA_RETOMADA",
        [TipoDoRegistroDoBloqueio.Retomada] = "RETOMADA",
        [TipoDoRegistroDoBloqueio.Desativacao] = "DESATIVACAO",
        [TipoDoRegistroDoBloqueio.Reativacao] = "REATIVACAO",
    };
}
