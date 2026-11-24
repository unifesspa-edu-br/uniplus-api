namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.CodigosDeContrato;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class SqlDosCodigosDeContratoTests
{
    [Fact(DisplayName = "A lista separa os literais por vírgula e espaço")]
    public void Lista_EnumComDoisCodigos_SeparaPorVirgulaEEspaco() =>
        SqlDosCodigosDeContrato.Lista<FormatoDeAula>().Should().Be("'EM_SALA', 'A_DISTANCIA'");

    [Fact(DisplayName = "A lista segue a ordem de declaração dos membros, e não a numérica nem a alfabética")]
    public void Lista_EnumDeclaradoForaDeOrdem_SegueAOrdemDeDeclaracao() =>
        SqlDosCodigosDeContrato.Lista<CodigoForaDeOrdem>().Should().Be("'ORDEM_Z', 'D_AGUA', 'ORDEM_A'");

    [Fact(DisplayName = "O literal escreve o código do valor entre aspas simples")]
    public void Literal_ValorComCodigo_EscreveOCodigoEntreAspas() =>
        SqlDosCodigosDeContrato.Literal(FormatoDeAula.Remoto).Should().Be("'A_DISTANCIA'");

    [Fact(DisplayName = "O literal escreve o código entre aspas simples")]
    public void Literal_OutroCodigo_EscreveEntreAspas() =>
        SqlDosCodigosDeContrato.Literal(CodigoForaDeOrdem.DoMeio).Should().Be("'D_AGUA'");

    [Fact(DisplayName = "O literal de valor que não é membro do enum falha, sem código inventado")]
    public void Literal_ValorForaDoEnum_Lanca() =>
        FluentActions.Invoking(() => SqlDosCodigosDeContrato.Literal((FormatoDeAula)99))
            .Should().Throw<ArgumentOutOfRangeException>();
}
