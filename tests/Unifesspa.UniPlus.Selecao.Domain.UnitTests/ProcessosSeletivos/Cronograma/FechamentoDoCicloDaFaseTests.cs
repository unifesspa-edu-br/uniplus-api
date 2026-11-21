namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Xunit;

using static ModeloDeFase;

/// <summary>A fase que publica o preliminar de um tipo de ato publica nela mesma o definitivo do mesmo tipo.</summary>
public sealed class FechamentoDoCicloDaFaseTests
{
    private static (string? Campo, string Codigo)[] Fechamento(params FaseDeclarada[] fases) =>
        [.. Cronograma(fases).ConferirFechamentoDoCiclo().Select(static r => (r.Field, r.Error.Code))];

    [Fact(DisplayName = "A fase que publica só o preliminar é recusada nos produtos, com o tipo de ato e a fase na mensagem")]
    public void PreliminarSemDefinitivo_RecusaNosProdutos()
    {
        FaseDeclarada fase = new ModeloDeFase("PROVA", 1).Publicando(Preliminar("GABARITO")).Declarar();

        FieldError recusa = Cronograma(fase).ConferirFechamentoDoCiclo().Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].produtos");
        recusa.Error.Code.Should().Be("ProcessoSeletivo.FaseComPreliminarSemDefinitivo");
        recusa.Error.Message.Should().Contain("'PROVA'").And.Contain("'GABARITO'");
    }

    [Fact(DisplayName = "O definitivo de outra fase, ou de outro tipo de ato, não fecha o ciclo: cada tipo de ato aberto gera uma recusa")]
    public void DefinitivoForaDoLugarOuDeOutroTipo_NaoFecha()
    {
        Fechamento(
            new ModeloDeFase("PROVA", 1).Publicando(Preliminar("GABARITO"), Definitivo("RESULTADO_PROVA"), Preliminar("RESULTADO_PROVA")).Declarar(),
            new ModeloDeFase("RESULTADO", 2).Publicando(Definitivo("GABARITO")).Declarar())
            .Should().Equal(("fases[0].produtos", "ProcessoSeletivo.FaseComPreliminarSemDefinitivo"));

        Fechamento(new ModeloDeFase("PROVA", 1).Publicando(Preliminar("A"), Preliminar("B")).Declarar())
            .Should().Equal(
                ("fases[0].produtos", "ProcessoSeletivo.FaseComPreliminarSemDefinitivo"),
                ("fases[0].produtos", "ProcessoSeletivo.FaseComPreliminarSemDefinitivo"));
    }

    [Fact(DisplayName = "A fase que publica o preliminar e o definitivo do mesmo tipo, ou só o definitivo, não é recusada")]
    public void CicloFechadoNaFase_NaoRecusa()
    {
        Fechamento(
            new ModeloDeFase("PROVA", 1).Publicando(Preliminar("GABARITO"), Definitivo("GABARITO")).Declarar(),
            new ModeloDeFase("RESULTADO", 2).Publicando(Definitivo("RESULTADO")).Declarar(),
            new ModeloDeFase("INSCRICAO", 3).Declarar()).Should().BeEmpty();
    }
}
