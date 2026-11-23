namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class ChavesDasFasesEEtapasTests
{
    [Fact]
    public async Task DefinirEtapas_TiposRepetidos_FormaChavesDistintas()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        EtapaProcessoInput primeira = EtapaInput("Primeira", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1);
        EtapaProcessoInput segunda = EtapaInput("Segunda", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 2);
        Result resultado = await ExecutarSobre(processo, null, primeira, segunda);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Select(etapa => etapa.Chave.Valor).Should().Equal("etapa_prova_objetiva", "etapa_prova_objetiva_2");
    }

    [Fact]
    public async Task DefinirEtapas_RegravacaoDaEtapaReconhecida_PreservaChave()
    {
        EtapaProcesso etapa = EtapaDeProvaObjetiva("Prova");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapa);
        ChaveLocal chave = etapa.Chave;
        EtapaProcessoInput entrada = EtapaInput("Renomeada", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapa.Id);
        Result resultado = await ExecutarSobre(processo, null, entrada);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Single().Chave.Should().Be(chave);
    }

    [Fact]
    public async Task DefinirFases_RegravacaoDaFaseReconhecida_PreservaChave()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput entrada = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());
        (await cenario.DefinirFasesAsync(entrada)).IsSuccess.Should().BeTrue();
        ChaveLocal chave = cenario.Processo.CronogramaFases.Single().Chave;
        (await cenario.DefinirFasesAsync(entrada)).IsSuccess.Should().BeTrue();
        cenario.Processo.CronogramaFases.Single().Chave.Should().Be(chave);
    }
}
