namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDeContratoDasBasesLegaisEReferenciaTests
{
    [Fact]
    public void ReferenciaTipo_CadaValor_TemCodigoERotuloEsperados()
    {
        Enum.GetValues<ReferenciaTipo>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<ReferenciaTipo, (string, string)>
            {
                [ReferenciaTipo.FimInscricao] = ("FIM_INSCRICAO", "Fim da inscrição"),
                [ReferenciaTipo.InicioFase] = ("INICIO_FASE", "Início da fase"),
                [ReferenciaTipo.FimFase] = ("FIM_FASE", "Fim da fase"),
                [ReferenciaTipo.DataEspecifica] = ("DATA_ESPECIFICA", "Data específica"),
            });
    }

    [Fact]
    public void AbrangenciaDoDiaNaoUtil_CadaValor_TemCodigoERotuloEsperados()
    {
        Enum.GetValues<AbrangenciaDoDiaNaoUtil>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<AbrangenciaDoDiaNaoUtil, (string, string)>
            {
                [AbrangenciaDoDiaNaoUtil.Nacional] = ("NACIONAL", "Nacional"),
                [AbrangenciaDoDiaNaoUtil.Estadual] = ("ESTADUAL", "Estadual"),
                [AbrangenciaDoDiaNaoUtil.Municipal] = ("MUNICIPAL", "Municipal"),
                [AbrangenciaDoDiaNaoUtil.Institucional] = ("INSTITUCIONAL", "Institucional"),
            });
    }
}
