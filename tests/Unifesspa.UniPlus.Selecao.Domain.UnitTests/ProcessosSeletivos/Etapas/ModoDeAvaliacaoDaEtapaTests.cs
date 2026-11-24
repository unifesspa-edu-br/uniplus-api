namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Xunit;

using static ModeloDeEtapa;

public sealed class ModoDeAvaliacaoDaEtapaTests
{
    private static readonly CitacaoDaEtapa SemCitacao = new(CitadaPeloDesempate: false, NotasMinimasDaEliminacao: []);

    [Fact(DisplayName = "A etapa cuja nota vem do ENEM é de nota do ENEM")]
    public void ModoDeAvaliacao_EtapaDoEnem_NotaDoEnem()
    {
        new ModeloDeEtapa("Nota do ENEM").DoEnem().Declarar().ModoDeAvaliacao(SemCitacao).Should().Be(ModoDeAvaliacaoDaEtapa.NotaDoEnem);
    }

    [Fact(DisplayName = "O conceito categórico decide o modo antes de qualquer sinal de nota")]
    public void ModoDeAvaliacao_ConceitoCategorico_ConceitoCategorico()
    {
        new ModeloDeEtapa("Entrevista").Categorica().Declarar().ModoDeAvaliacao(SemCitacao).Should().Be(ModoDeAvaliacaoDaEtapa.ConceitoCategorico);
    }

    [Fact(DisplayName = "Conceito numérico conta como nota numérica")]
    public void ModoDeAvaliacao_ConceitoNumerico_NotaNumerica()
    {
        ModeloDeEtapa modelo = new ModeloDeEtapa("Redação").SemNota() with { Conceito = NotaDeZeroADez() };

        modelo.Declarar().ModoDeAvaliacao(SemCitacao).Should().Be(ModoDeAvaliacaoDaEtapa.NotaNumerica);
    }

    [Fact(DisplayName = "Compor a nota, ter nota mínima ou ser citada por nota tornam a etapa de nota numérica")]
    public void ModoDeAvaliacao_SinaisDeNota_NotaNumerica()
    {
        EtapaDeclarada compoe = new ModeloDeEtapa("Prova").Declarar();
        EtapaDeclarada comNotaMinima = (new ModeloDeEtapa("Prova").SemNota() with { NotaMinima = 5m }).Declarar();
        EtapaDeclarada citadaNoDesempate = new ModeloDeEtapa("Prova").SemNota().Declarar();
        EtapaDeclarada citadaNaEliminacao = new ModeloDeEtapa("Prova").SemNota().Declarar();

        compoe.ModoDeAvaliacao(SemCitacao).Should().Be(ModoDeAvaliacaoDaEtapa.NotaNumerica);
        comNotaMinima.ModoDeAvaliacao(SemCitacao).Should().Be(ModoDeAvaliacaoDaEtapa.NotaNumerica);
        citadaNoDesempate.ModoDeAvaliacao(new CitacaoDaEtapa(true, [])).Should().Be(ModoDeAvaliacaoDaEtapa.NotaNumerica);
        citadaNaEliminacao.ModoDeAvaliacao(new CitacaoDaEtapa(false, [6m])).Should().Be(ModoDeAvaliacaoDaEtapa.NotaNumerica);
    }

    [Fact(DisplayName = "Sem nota, sem conceito e sem citação a etapa é sem nota nem conceito")]
    public void ModoDeAvaliacao_SemSinais_SemNotaNemConceito()
    {
        new ModeloDeEtapa("Triagem").SemNota().Declarar().ModoDeAvaliacao(SemCitacao).Should().Be(ModoDeAvaliacaoDaEtapa.SemNotaNemConceito);
    }

    [Theory(DisplayName = "O modo fica indeterminado quando um dado que o decide foi recusado")]
    [InlineData("carater")]
    [InlineData("peso")]
    [InlineData("notaMinima")]
    [InlineData("conceito")]
    public void ModoDeterminado_DadoDecisivoRecusado_FicaIndeterminado(string dadoRecusado)
    {
        ModeloDeEtapa modelo = new("Prova");
        modelo = dadoRecusado switch
        {
            "carater" => modelo with { Carater = (CaraterEtapa)99 },
            "peso" => modelo with { Peso = -1m },
            "notaMinima" => modelo with { NotaMinima = -1m },
            _ => modelo.DoEnem() with { Conceito = NotaDeZeroADez() },
        };

        EtapaDeclarada etapa = modelo.Declarar();

        etapa.ModoDeterminado.Should().BeFalse();
        Action pedirOModo = () => etapa.ModoDeAvaliacao(SemCitacao);
        pedirOModo.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Recusa de dado que não decide o modo não o torna indeterminado")]
    public void ModoDeterminado_RecusaDeDadoQueNaoDecide_ContinuaDeterminado()
    {
        EtapaDeclarada etapa = (new ModeloDeEtapa("Prova") with { NotaMaxima = -1m, Ordem = 0 }).Declarar();

        etapa.Recusas.Should().HaveCount(2);
        etapa.ModoDeterminado.Should().BeTrue();
        etapa.ModoDeAvaliacao(SemCitacao).Should().Be(ModoDeAvaliacaoDaEtapa.NotaNumerica);
    }
}
