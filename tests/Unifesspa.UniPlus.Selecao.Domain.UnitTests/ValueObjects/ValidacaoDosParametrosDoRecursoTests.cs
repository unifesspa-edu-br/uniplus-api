namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

public sealed class ValidacaoDosParametrosDoRecursoTests
{
    private static readonly ParametrosRegraPrazoRecurso Valido = new(1m, UnidadePrazo.Horas, null, null, false, null, null);

    private static ParametrosRegraPrazoRecurso ParametrosComCausasIndependentes => Valido with
    {
        PrazoValor = 0,
        PrazoUnidade = UnidadePrazo.Nenhuma,
        SuspensividadePrimeiraInstanciaValor = 0,
        SuspensividadePrimeiraInstanciaUnidade = UnidadePrazo.Nenhuma,
        AdmiteInstanciaSuperior = true,
        SuspensividadeSegundaInstanciaValor = 0,
        SuspensividadeSegundaInstanciaUnidade = UnidadePrazo.Nenhuma
    };

    [Fact]
    public void Validar_CausasIndependentes_RetornaEmOrdemComPrecedencia()
    {
        ValidacaoDosParametrosDoRecurso.Validar(ParametrosComCausasIndependentes).Select(static recusa => recusa.Motivo).Should().Equal(
            MotivoDaRecusaDosParametros.PrazoNaoPositivo,
            MotivoDaRecusaDosParametros.SuspensividadeNaoPositiva,
            MotivoDaRecusaDosParametros.SuspensividadeNaoPositiva);
    }

    [Fact]
    public void Validar_UmaCausa_RetornaUmaRecusa() =>
        ValidacaoDosParametrosDoRecurso.Validar(Valido with { SuspensividadePrimeiraInstanciaValor = 1 })
            .Select(static recusa => recusa.Motivo).Should().Equal(MotivoDaRecusaDosParametros.SuspensividadeIncompleta);

    [Fact]
    public void Validar_ParametrosValidos_NaoRecusa() => ValidacaoDosParametrosDoRecurso.Validar(Valido).Should().BeEmpty();
}
