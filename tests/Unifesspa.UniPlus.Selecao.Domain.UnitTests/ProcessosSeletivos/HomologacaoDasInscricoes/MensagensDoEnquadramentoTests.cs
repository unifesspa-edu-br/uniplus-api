namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

public sealed class MensagensDoEnquadramentoTests
{
    [Theory]
    [InlineData("CondicaoDnf.FatoObrigatorio", "Informe o fato da condição.")]
    [InlineData("CondicaoDnf.OperadorInvalido", "O operador informado não é reconhecido.")]
    [InlineData("CondicaoDnf.FormaIncoerenteComOperador", "O formato do valor não corresponde ao operador informado.")]
    [InlineData("PredicadoDnf.FormaJsonInvalida", null)]
    [InlineData("ClausulaDnf.ClausulaVazia", null)]
    [InlineData("teste.desconhecido", null)]
    public void DoDefeitoDeForma_CodigoRecebido_DevolveMensagemConhecidaOuAusencia(string codigo, string? mensagem)
    {
        MensagensDoEnquadramento.DoDefeitoDeForma(codigo).Should().Be(mensagem);
    }
}
