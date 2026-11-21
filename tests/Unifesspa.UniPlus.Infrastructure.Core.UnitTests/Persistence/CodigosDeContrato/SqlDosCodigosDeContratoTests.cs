namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.CodigosDeContrato;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class SqlDosCodigosDeContratoTests
{
    [Fact(DisplayName = "A lista separa os literais por vírgula e espaço")]
    public void Lista_EnumComDoisCodigos_SeparaPorVirgulaEEspaco() =>
        SqlDosCodigosDeContrato.Lista<FormatoDeAula>().Should().Be("'em_sala', 'a_distancia'");

    [Fact(DisplayName = "A lista segue a ordem de declaração dos membros, e não a numérica nem a alfabética")]
    public void Lista_EnumDeclaradoForaDeOrdem_SegueAOrdemDeDeclaracao() =>
        SqlDosCodigosDeContrato.Lista<CodigoComAspa>().Should().Be("'ordem_z', 'd''agua', 'ordem_a'");

    [Fact(DisplayName = "O literal escreve o código do valor entre aspas simples")]
    public void Literal_ValorComCodigo_EscreveOCodigoEntreAspas() =>
        SqlDosCodigosDeContrato.Literal(FormatoDeAula.Remoto).Should().Be("'a_distancia'");

    [Fact(DisplayName = "O literal dobra a aspa simples do código")]
    public void Literal_CodigoComAspa_DobraAAspa() =>
        SqlDosCodigosDeContrato.Literal(CodigoComAspa.ComAspa).Should().Be("'d''agua'");

    [Fact(DisplayName = "O literal de valor que não é membro do enum falha, sem código inventado")]
    public void Literal_ValorForaDoEnum_Lanca() =>
        FluentActions.Invoking(() => SqlDosCodigosDeContrato.Literal((FormatoDeAula)99))
            .Should().Throw<ArgumentOutOfRangeException>();
}
