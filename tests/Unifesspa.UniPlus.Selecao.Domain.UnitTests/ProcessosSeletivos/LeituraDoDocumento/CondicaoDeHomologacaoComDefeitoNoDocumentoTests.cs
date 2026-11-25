namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

public sealed class CondicaoDeHomologacaoComDefeitoNoDocumentoTests
{
    [Fact]
    public void Ler_CondicaoEEnquadramentoInvalidos_AcumulaCausasNaOrdem()
    {
        var item = new { codigoDoDado = CondicaoDeHomologacaoComDefeitoDeTeste.Dado, operador = "ALGUM_DE", valor = CondicaoDeHomologacaoComDefeitoDeTeste.Valor };
        CondicaoNoDocumento condicao = Condicao(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, CondicaoDeHomologacaoComDefeitoDeTeste.Edicao, CondicaoDeHomologacaoComDefeitoDeTeste.Motivo) with { Enquadramento = JsonSerializer.SerializeToElement(new[] { new[] { item } }) };
        FaseNoDocumento fase = Fase(0, "homologacao", "RESULTADO") with { Publicacoes = new([], false, null, new RegraDeHomologacaoNoDocumento(AtoDoResultado, [new([condicao])])) };
        IdentificacaoECronogramaLidos lidos = Ler(Documento(fase));
        const string caminho = "fases[0].regraDeHomologacao.alternativas[0].condicoes[0].";
        PartesRecusadas(lidos).Select(parte => (parte.Codigo, parte.Ponteiro)).Should().Equal(
            CondicaoDeHomologacaoComDefeitoDeTeste.Causas.Select(causa => (causa.Codigo, PonteiroNoDocumento.Converter(caminho + causa.Campo, lidos.Origem))));
    }
}
