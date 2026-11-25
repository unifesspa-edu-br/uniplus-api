namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

public sealed class MensagensDoEnquadramentoNoDocumentoTests
{
    [Theory]
    [InlineData("", "IGUAL", "CondicaoDnf.FatoObrigatorio")]
    [InlineData("DADO_FICTICIO", "DESCONHECIDO", "CondicaoDnf.OperadorInvalido")]
    [InlineData("DADO_FICTICIO", "ALGUM_DE", "CondicaoDnf.FormaIncoerenteComOperador")]
    public void Ler_DefeitoDeForma_UsaMensagemDoDominio(string dado, string operador, string codigo)
    {
        var item = new { codigoDoDado = dado, operador, valor = "opção fictícia" };
        CondicaoNoDocumento condicao = Condicao(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2025, "MOTIVO_FICTICIO") with { Enquadramento = JsonSerializer.SerializeToElement(new[] { new[] { item } }) };
        FaseNoDocumento fase = Fase(0, "homologacao", "RESULTADO") with { Publicacoes = new([], false, null, new RegraDeHomologacaoNoDocumento(AtoDoResultado, [new([condicao])])) };
        IdentificacaoECronogramaLidos lidos = Ler(Documento(fase));
        PartesRecusadas(lidos).Select(parte => parte.Codigo).Should().Equal(codigo);
        Mensagens(lidos).Should().Equal(MensagensDoEnquadramento.DoDefeitoDeForma(codigo));
    }
}
