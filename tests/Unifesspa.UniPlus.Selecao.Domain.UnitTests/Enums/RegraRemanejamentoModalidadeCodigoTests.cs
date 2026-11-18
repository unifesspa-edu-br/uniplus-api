namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class RegraRemanejamentoModalidadeCodigoTests
{
    [Fact(DisplayName = "Token nulo gravado é a modalidade que não remaneja")]
    public void DoTokenGravado_Nulo_NaoRemaneja()
    {
        RegraRemanejamentoModalidadeCodigo.DoTokenGravado(null).Should().Be(RegraRemanejamentoModalidade.Nenhuma);
    }

    [Theory(DisplayName = "Token do vocabulário do cadastro volta como a regra de remanejamento")]
    [InlineData("SEGUE_CASCATA", RegraRemanejamentoModalidade.SegueCascata)]
    [InlineData("DESTINO_UNICO", RegraRemanejamentoModalidade.DestinoUnico)]
    [InlineData("CRUZADO", RegraRemanejamentoModalidade.Cruzado)]
    public void DoTokenGravado_TokenConhecido_DevolveARegra(string token, RegraRemanejamentoModalidade esperada)
    {
        RegraRemanejamentoModalidadeCodigo.DoTokenGravado(token).Should().Be(esperada);
    }

    [Theory(DisplayName = "Token fora do vocabulário gravado na coluna é recusado, e não lido como não remaneja")]
    [InlineData("")]
    [InlineData("QUALQUER_COISA")]
    [InlineData("segue_cascata")]
    public void DoTokenGravado_TokenDesconhecido_LancaArgumentOutOfRange(string token)
    {
        Action ler = () => RegraRemanejamentoModalidadeCodigo.DoTokenGravado(token);

        ler.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("codigo");
    }
}
