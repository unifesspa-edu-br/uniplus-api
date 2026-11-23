namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ChavesDasBancasNaEdicaoTests
{
    [Fact]
    public void DasEtapas_ComBancaDaFase_ReservaAChaveDaFase()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaFase(3);
        ChavesDasBancasDoProcesso chaves = ChavesDasBancasNaEdicao.DasEtapas(processo, []);

        chaves.Formar("BANCA").Valor.Should().Be("banca_2");
    }

    [Fact]
    public void DasFases_ComBancaDaEtapa_ReservaAChaveDaEtapa()
    {
        ProcessoSeletivo processo = ProcessoComCorrecaoIndependente.ComBancaDaFase(3);
        ChavesDasBancasDoProcesso chaves = ChavesDasBancasNaEdicao.DasFases(processo, []);

        chaves.Formar("BANCA").Valor.Should().Be("banca_2");
    }
}
