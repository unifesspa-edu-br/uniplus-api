namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A promessa de parecer individual ausente é recusada como obrigatória, no campo da fase, e não vira "não promete" em silêncio:
/// a fase não é gravada e a recusa sai junto das demais da mesma fase.
/// </summary>
public sealed class DefinirCronogramaFasesParecerIndividualAusenteTests
{
    private static (CenarioDoCronogramaDeFases Cenario, FaseCronogramaInput Fase) CenarioComUmaFase()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Ordem = 1 };
        return (cenario, fase);
    }

    [Fact(DisplayName = "A promessa de parecer ausente é recusada no campo da fase e nada é gravado")]
    public async Task Handle_ParecerAusente_RecusaComoObrigatorio()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput fase) = CenarioComUmaFase();

        Result resultado = await cenario.DefinirFasesAsync(fase with { EmiteParecerIndividual = null });

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("fases[0].emiteParecerIndividual", CodigosDeValidacao.Obrigatorio));
        cenario.Processo.CronogramaFases.Should().BeEmpty();
    }

    [Fact(DisplayName = "A promessa de parecer ausente sai junto da recusa do período invertido da mesma fase, e primeiro")]
    public async Task Handle_ParecerAusenteEPeriodoInvertido_AcumulaAsDuas()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput fase) = CenarioComUmaFase();
        FaseCronogramaInput invertida = fase with
        {
            EmiteParecerIndividual = null,
            Inicio = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        };

        Result resultado = await cenario.DefinirFasesAsync(invertida);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(CodigosDeValidacao.Obrigatorio, "FaseCronograma.JanelaInvertida");
    }

    [Fact(DisplayName = "A promessa de parecer ausente sai junto da recusa de um produto da mesma fase")]
    public async Task Handle_ParecerAusenteEProdutoRecusado_AcumulaAsDuas()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput fase) = CenarioComUmaFase();
        FaseCronogramaInput comProdutoRecusado = fase with
        {
            EmiteParecerIndividual = null,
            Produtos = [new ProdutoDaFaseInput(AtoDaClassificacao, "PAPEL_QUE_NAO_EXISTE")],
        };

        Result resultado = await cenario.DefinirFasesAsync(comProdutoRecusado);

        resultado.Errors.Select(static e => e.Field).Should()
            .BeEquivalentTo(["fases[0].emiteParecerIndividual", "fases[0].produtos[0].papel"]);
        cenario.Processo.CronogramaFases.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase canônica inexistente da primeira fase não oculta o parecer ausente da fase seguinte")]
    public async Task Handle_FaseCanonicaInexistenteEParecerAusenteNaSeguinte_AcumulaAsDuas()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput fase) = CenarioComUmaFase();
        FaseCronogramaInput inexistente = EntradaDaFase(Guid.CreateVersion7()) with { Ordem = 1 };

        Result resultado = await cenario.DefinirFasesAsync(inexistente, fase with { Ordem = 2, EmiteParecerIndividual = null });

        resultado.Errors.Select(static e => e.Field).Should()
            .Equal("fases[0].faseCanonicaId", "fases[1].emiteParecerIndividual");
    }

    [Fact(DisplayName = "A promessa de parecer declarada falsa é uma escolha e a fase é gravada")]
    public async Task Handle_ParecerFalso_Grava()
    {
        (CenarioDoCronogramaDeFases cenario, FaseCronogramaInput fase) = CenarioComUmaFase();

        Result resultado = await cenario.DefinirFasesAsync(fase with { EmiteParecerIndividual = false });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Single().EmiteParecerIndividual.Should().BeFalse();
    }
}
