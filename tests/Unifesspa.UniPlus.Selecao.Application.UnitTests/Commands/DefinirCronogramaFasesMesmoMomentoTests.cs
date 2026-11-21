namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A marca "no mesmo momento da anterior" na gravação do cronograma: é gravada, é substituição integral, não vale na primeira fase
/// e não vale entre fases ligadas por precedência no cadastro.
/// </summary>
public sealed class DefinirCronogramaFasesMesmoMomentoTests
{
    private static (CenarioDoCronogramaDeFases Cenario, FaseCronogramaInput Preliminar, FaseCronogramaInput Final) CenarioComDuasFases()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao()).ComTipoDeAto(TipoAtoDoComunicado());
        cenario.Processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        FaseCronogramaInput preliminar = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Ordem = 1 };
        FaseCronogramaInput final = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with
        {
            Ordem = 2,
            Produtos = [ProdutoDoComunicado(null)],
            Inicio = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
        };

        return (cenario, preliminar, final);
    }

    [Fact(DisplayName = "A marca na segunda fase é gravada e a fase sem a marca fica sem ela")]
    public async Task Handle_MarcaNaSegundaFase_EGravada()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput primeira, FaseCronogramaInput segunda) = CenarioComDuasFases();

        Result resultado = await cenario.DefinirFasesAsync(primeira, segunda with { NoMesmoMomentoDaAnterior = true });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.OrderBy(static fase => fase.Ordem).Select(static fase => fase.NoMesmoMomentoDaAnterior)
            .Should().Equal(false, true);
    }

    [Fact(DisplayName = "Gravar de novo sem a marca a retira: a gravação substitui o cronograma inteiro")]
    public async Task Handle_GravarSemAMarca_RetiraAMarca()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput primeira, FaseCronogramaInput segunda) = CenarioComDuasFases();
        (await cenario.DefinirFasesAsync(primeira, segunda with { NoMesmoMomentoDaAnterior = true })).IsSuccess.Should().BeTrue();

        Result resultado = await cenario.DefinirFasesAsync(primeira, segunda);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Should().OnlyContain(static fase => !fase.NoMesmoMomentoDaAnterior);
    }

    [Fact(DisplayName = "A marca na primeira fase é recusada no campo da marca, sem identificar outra fase")]
    public async Task Handle_MarcaNaPrimeiraFase_Recusa()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput primeira, FaseCronogramaInput segunda) = CenarioComDuasFases();

        Result resultado = await cenario.DefinirFasesAsync(primeira with { NoMesmoMomentoDaAnterior = true }, segunda);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].noMesmoMomentoDaAnterior");
        recusa.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.PrimeiraFaseNoMesmoMomentoDaAnterior);
        recusa.Identificadores.Should().BeNull();
    }

    [Fact(DisplayName = "Fases ligadas por precedência no cadastro e marcadas no mesmo momento são recusadas na fase marcada")]
    public async Task Handle_FasesLigadasPorPrecedenciaNoMesmoMomento_Recusa()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput primeira, FaseCronogramaInput segunda) = CenarioComDuasFases();
        cenario.ComPrecedencias(("RESULTADO_FINAL", "RESULTADO_PRELIMINAR", true));

        Result resultado = await cenario.DefinirFasesAsync(primeira, segunda with { NoMesmoMomentoDaAnterior = true });

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[1].noMesmoMomentoDaAnterior");
        recusa.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.FasesComPrecedenciaNoMesmoMomento);
        (await cenario.DefinirFasesAsync(primeira, segunda)).IsSuccess.Should().BeTrue();
    }
}
