namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

public sealed class EscalaDoLancamentoTests
{
    private static (string? Campo, string Codigo)[] Pares(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static e => (e.Field, e.Error.Code))];

    [Theory(DisplayName = "A nota máxima zero ou negativa é recusada")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validar_NotaMaximaNaoPositiva_Recusa(int notaMaxima)
    {
        List<FieldError> erros = EscalaDoLancamento.Validar(notaMinimaAceita: null, notaMaxima, passoDoLancamento: null);

        Pares(erros).Should().Equal(("notaMaxima", EtapaProcessoErrorCodes.NotaMaximaInvalida));
    }

    [Theory(DisplayName = "O passo do lançamento zero ou negativo é recusado")]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Validar_PassoNaoPositivo_Recusa(double passo)
    {
        List<FieldError> erros = EscalaDoLancamento.Validar(notaMinimaAceita: null, notaMaxima: 10m, (decimal)passo);

        Pares(erros).Should().Equal(("passoDoLancamento", EtapaProcessoErrorCodes.PassoDoLancamentoInvalido));
    }

    [Fact(DisplayName = "A nota mínima acima da nota máxima é recusada no campo da nota mínima")]
    public void Validar_NotaMinimaAcimaDaNotaMaxima_Recusa()
    {
        List<FieldError> erros = EscalaDoLancamento.Validar(notaMinimaAceita: 10.01m, notaMaxima: 10m, passoDoLancamento: null);

        Pares(erros).Should().Equal(("notaMinima", EtapaProcessoErrorCodes.NotaMinimaAcimaDaNotaMaxima));
    }

    [Fact(DisplayName = "A nota mínima igual à nota máxima é aceita")]
    public void Validar_NotaMinimaIgualANotaMaxima_Aceita()
    {
        EscalaDoLancamento.Validar(notaMinimaAceita: 10m, notaMaxima: 10m, passoDoLancamento: null).Should().BeEmpty();
    }

    [Fact(DisplayName = "A nota mínima já recusada chega nula e não é comparada com a nota máxima")]
    public void Validar_NotaMinimaJaRecusada_NaoERepetida()
    {
        EscalaDoLancamento.Validar(notaMinimaAceita: null, notaMaxima: 10m, passoDoLancamento: null).Should().BeEmpty();
    }

    [Theory(DisplayName = "A nota máxima precisa ser múltipla do passo do lançamento")]
    [InlineData(7.5, 0.5, true)]
    [InlineData(7.5, 0.4, false)]
    [InlineData(0.3, 0.1, true)]
    [InlineData(10, 10, true)]
    [InlineData(10, 20, false)]
    public void Validar_NotaMaximaContraPasso_RecusaForaDoMultiplo(double notaMaxima, double passo, bool aceita)
    {
        List<FieldError> erros = EscalaDoLancamento.Validar(notaMinimaAceita: null, (decimal)notaMaxima, (decimal)passo);

        erros.Count.Should().Be(aceita ? 0 : 1);
    }

    [Fact(DisplayName = "A nota máxima inválida não é comparada com a nota mínima nem com o passo")]
    public void Validar_NotaMaximaInvalida_NaoEComparada()
    {
        List<FieldError> erros = EscalaDoLancamento.Validar(notaMinimaAceita: 5m, notaMaxima: 0m, passoDoLancamento: 0.3m);

        Pares(erros).Should().Equal(("notaMaxima", EtapaProcessoErrorCodes.NotaMaximaInvalida));
    }

    [Fact(DisplayName = "Sem nota máxima declarada nada é comparado")]
    public void Validar_SemNotaMaxima_NaoCompara()
    {
        EscalaDoLancamento.Validar(notaMinimaAceita: 50m, notaMaxima: null, passoDoLancamento: 0.3m).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nota mínima acima da máxima e máxima fora do passo acumulam, a mínima primeiro")]
    public void Validar_MinimaAcimaEMaximaForaDoPasso_AcumulamNaOrdem()
    {
        List<FieldError> erros = EscalaDoLancamento.Validar(notaMinimaAceita: 12m, notaMaxima: 10m, passoDoLancamento: 0.3m);

        Pares(erros).Should().Equal(
            ("notaMinima", EtapaProcessoErrorCodes.NotaMinimaAcimaDaNotaMaxima),
            ("notaMaxima", EtapaProcessoErrorCodes.NotaMaximaForaDoPasso));
    }

    [Fact(DisplayName = "As recusas dos valores isolados vêm antes das comparações")]
    public void Validar_PassoInvalidoComNotaMaximaValida_RecusaSoOPasso()
    {
        List<FieldError> erros = EscalaDoLancamento.Validar(notaMinimaAceita: 20m, notaMaxima: 10m, passoDoLancamento: 0m);

        Pares(erros).Should().Equal(
            ("passoDoLancamento", EtapaProcessoErrorCodes.PassoDoLancamentoInvalido),
            ("notaMinima", EtapaProcessoErrorCodes.NotaMinimaAcimaDaNotaMaxima));
    }
}
