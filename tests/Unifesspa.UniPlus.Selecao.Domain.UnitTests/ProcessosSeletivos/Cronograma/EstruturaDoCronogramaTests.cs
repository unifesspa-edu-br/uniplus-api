namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Xunit;

using static ModeloDeFase;

public sealed class EstruturaDoCronogramaTests
{
    private static (string? Campo, string Codigo)[] Estrutura(params FaseDeclarada[] fases) =>
        [.. Cronograma(fases).ConferirEstrutura().Select(static r => (r.Field, r.Error.Code))];

    [Fact(DisplayName = "Cronograma sem fases é recusado")]
    public void ConferirEstrutura_SemFases_Recusa()
    {
        Estrutura().Should().Equal(("fases", "ProcessoSeletivo.CronogramaFasesVazio"));
    }

    [Fact(DisplayName = "Cronograma de fases distintas e ordens distintas não tem causa de estrutura")]
    public void ConferirEstrutura_FasesDistintas_NaoRecusa()
    {
        Estrutura(new ModeloDeFase("A", 1).Declarar(), new ModeloDeFase("B", 2).Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada ordem repetida é recusada a partir da segunda ocorrência")]
    public void ConferirEstrutura_OrdemRepetida_RecusaDaSegundaEmDiante()
    {
        Estrutura(
            new ModeloDeFase("A", 1).Declarar(),
            new ModeloDeFase("B", 1).Declarar(),
            new ModeloDeFase("C", 1).Declarar())
            .Should().Equal(
                ("fases[1].ordem", "ProcessoSeletivo.OrdemFaseDuplicada"),
                ("fases[2].ordem", "ProcessoSeletivo.OrdemFaseDuplicada"));
    }

    [Fact(DisplayName = "A mesma fase canônica duas vezes é recusada na segunda ocorrência")]
    public void ConferirEstrutura_MesmaOrigem_RecusaASegunda()
    {
        Guid origem = Guid.CreateVersion7();

        Estrutura(
            new ModeloDeFase("A", 1) { OrigemId = origem }.Declarar(),
            new ModeloDeFase("OUTRO", 2) { OrigemId = origem }.Declarar())
            .Should().Equal(("fases[1].faseCanonicaId", "ProcessoSeletivo.FaseCanonicaDuplicada"));
    }

    [Fact(DisplayName = "O mesmo código com origens diferentes também é fase canônica duplicada")]
    public void ConferirEstrutura_MesmoCodigoComOrigensDiferentes_RecusaASegunda()
    {
        Estrutura(new ModeloDeFase("A", 1).Declarar(), new ModeloDeFase("A", 2).Declarar())
            .Should().Equal(("fases[1].faseCanonicaId", "ProcessoSeletivo.FaseCanonicaDuplicada"));
    }

    [Fact(DisplayName = "As causas saem por assunto e, dentro dela, pela posição de entrada")]
    public void ConferirEstrutura_VariosAssuntos_SaemNaOrdemDosAssuntos()
    {
        Estrutura(
            new ModeloDeFase("A", 1).Declarar(),
            new ModeloDeFase("A", 1).Declarar())
            .Should().Equal(
                ("fases[1].ordem", "ProcessoSeletivo.OrdemFaseDuplicada"),
                ("fases[1].faseCanonicaId", "ProcessoSeletivo.FaseCanonicaDuplicada"));
    }
}
