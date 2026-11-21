namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.VersoesPublicadasDeTeste;

public sealed class InicioDaAvaliacaoNoLugarTests
{
    private static readonly DateTimeOffset VigenciaDaVersao = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InicioDoPeriodo = VigenciaDaVersao.AddMonths(2);

    [Theory(DisplayName = "A avaliação começa no primeiro instante entre o período da própria fase e o registro no lugar")]
    [InlineData("HOMOLOGACAO_INSCRICOES", -3)]
    [InlineData("HOMOLOGACAO_INSCRICOES", 3)]
    [InlineData("ISENCAO", -3)]
    [InlineData("ISENCAO", 3)]
    public void FaseComRegistro_ComecaNoPrimeiroInstante(string codigo, int diasDoRegistro)
    {
        FaseDaVersaoPublicada fase = UmaFase(codigo);
        DateTimeOffset registro = InicioDoPeriodo.AddDays(diasDoRegistro);
        FatosDoInicioDasFases fatos = SemFatos with { AvaliacoesIniciadas = [new AvaliacaoIniciadaNoLugar(LugarDaDecisao.DaFase(fase.FaseCanonicaOrigemId), registro)] };
        InicioDasFases inicio = Inicio([VersaoDaFase(fase)], fatos);
        DateTimeOffset primeiro = registro < InicioDoPeriodo ? registro : InicioDoPeriodo;

        inicio.FasesProdutoras(primeiro.AddTicks(-1)).Should().BeEmpty();
        inicio.FasesProdutoras(primeiro).Should().ContainSingle().Which.Should().Be(fase.FaseCanonicaOrigemId);
    }

    [Theory(DisplayName = "Sem registro, a avaliação começa no início do período da própria fase")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    [InlineData("ISENCAO")]
    public void FaseSemRegistro_ComecaNoInicioDoPeriodo(string codigo)
    {
        FaseDaVersaoPublicada fase = UmaFase(codigo);
        InicioDasFases inicio = Inicio([VersaoDaFase(fase)], SemFatos);

        inicio.FasesProdutoras(InicioDoPeriodo.AddTicks(-1)).Should().BeEmpty();
        inicio.FasesProdutoras(InicioDoPeriodo).Should().ContainSingle().Which.Should().Be(fase.FaseCanonicaOrigemId);
    }

    [Fact(DisplayName = "Sem fase nem registro a avaliação não começou, mesmo no maior instante")]
    public void SemFaseNemRegistro_NaoComecou()
    {
        HashSet<Guid> produtoras = Inicio([], SemFatos).FasesProdutoras(DateTimeOffset.MaxValue);
        FasesIniciadas iniciadas = new([], fasesProdutoras: produtoras);

        produtoras.Should().BeEmpty();
        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(Guid.CreateVersion7())).Should().BeFalse();
    }

    private static FaseDaVersaoPublicada UmaFase(string codigo) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), codigo, null, new JanelaDeTempo(InicioDoPeriodo, null), ColetaInscricao: false, []);

    private static FasesDaVersaoPublicada VersaoDaFase(FaseDaVersaoPublicada fase) => new(Guid.CreateVersion7(), VigenciaDaVersao, null, [fase]);
}
