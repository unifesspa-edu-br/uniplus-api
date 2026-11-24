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
    public void TipoAbrangencia_CadaValor_TemCodigoERotuloEsperados()
    {
        Enum.GetValues<TipoAbrangencia>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<TipoAbrangencia, (string, string)>
            {
                [TipoAbrangencia.Federal] = ("FEDERAL", "Federal"),
                [TipoAbrangencia.Estadual] = ("ESTADUAL", "Estadual"),
                [TipoAbrangencia.Municipal] = ("MUNICIPAL", "Municipal"),
                [TipoAbrangencia.InternaNorma] = ("INTERNA_NORMA", "Norma interna"),
                [TipoAbrangencia.InternaEdital] = ("INTERNA_EDITAL", "Edital"),
            });
    }

    [Fact]
    public void StatusBaseLegal_CadaValor_TemCodigoERotuloEsperados()
    {
        Enum.GetValues<StatusBaseLegal>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<StatusBaseLegal, (string, string)>
            {
                [StatusBaseLegal.Pendente] = ("PENDENTE", "Pendente"),
                [StatusBaseLegal.Resolvido] = ("RESOLVIDO", "Resolvido"),
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
