namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;

/// <summary>
/// A fase que sai do cronograma leva junto os motivos dela.
/// </summary>
public sealed class MotivosDosLugaresDoCronogramaTests
{
    [Fact(DisplayName = "No rascunho, a fase que sai do cronograma leva os motivos escolhidos nela")]
    public async Task Rascunho_FaseQueSai_LevaOsMotivos()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput queFica = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());
        FaseCronogramaInput queSai = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with { Ordem = 2 };
        (await cenario.DefinirFasesAsync(queFica, queSai)).IsSuccess.Should().BeTrue();
        MotivoDoProcesso naFaseQueSai = Escolhido(cenario.Processo, LugarDaDecisao.DaFase(queSai.FaseCanonicaId), Motivo());
        cenario.MotivosEscolhidos.ListarEscolhaAtualAsync(cenario.Processo.Id, Arg.Any<CancellationToken>()).Returns([naFaseQueSai]);

        Result resultado = await cenario.DefinirFasesAsync(queFica);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.MotivosEscolhidos.Received(1).Remover(naFaseQueSai);
    }
}
