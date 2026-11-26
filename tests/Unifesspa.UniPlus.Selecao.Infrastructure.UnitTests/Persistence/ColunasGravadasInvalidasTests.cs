namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// A coluna gravada que não é um documento JSON legível, ou que guarda <c>null</c> onde o valor é obrigatório, é recusada na
/// leitura com mensagem fixa: nem o texto gravado nem a exceção da biblioteca saem na mensagem.
/// </summary>
public sealed class ColunasGravadasInvalidasTests
{
    private const string Marcador = "valor-gravado-ficticio-7f3a";
    private const string DocumentoMalformado = "{\"" + Marcador + "\":";
    private const string NuloGravado = "null";

    [Theory(DisplayName = "O documento malformado é recusado com mensagem fixa, sem o texto gravado")]
    [InlineData(typeof(CertameDivulgado), nameof(CertameDivulgado.AtosDivulgados), nameof(CertameDivulgado.AtosDivulgados))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Respostas), nameof(ComprovanteInscricao.Respostas))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Derivados), nameof(ComprovanteInscricao.Derivados))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Grupos), nameof(ComprovanteInscricao.Grupos))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Aceites), nameof(ComprovanteInscricao.Aceites))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Documentos), nameof(ComprovanteInscricao.Documentos))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Modalidades), nameof(ComprovanteInscricao.Modalidades))]
    [InlineData(typeof(FormatosPermitidos), nameof(FormatosPermitidos.Lista), nameof(DocumentoExigido.FormatosPermitidos))]
    [InlineData(typeof(DistincaoDasApresentacoes), nameof(DistincaoDasApresentacoes.OcorrenciasEsperadas), nameof(DistincaoDasApresentacoes.OcorrenciasEsperadas))]
    [InlineData(typeof(CondicaoGatilho), nameof(CondicaoGatilho.Valor), nameof(CondicaoGatilho.Valor))]
    [InlineData(typeof(CondicaoPrecondicaoFato), nameof(CondicaoPrecondicaoFato.Valor), nameof(CondicaoPrecondicaoFato.Valor))]
    [InlineData(typeof(CondicaoRegraDerivacao), nameof(CondicaoRegraDerivacao.Valor), nameof(CondicaoRegraDerivacao.Valor))]
    [InlineData(typeof(RegraCatalogo), nameof(RegraCatalogo.DeclaracaoDaRegra), nameof(RegraCatalogo))]
    [InlineData(typeof(RegraCatalogo), nameof(RegraCatalogo.Invariantes), nameof(RegraCatalogo))]
    public void Leitura_DocumentoMalformado_RecusaComMensagemFixa(Type entidade, string propriedade, string nomeDoValor)
    {
        Action leitura = () => Conversor(entidade, propriedade).ConvertFromProvider(DocumentoMalformado);

        leitura.DeveRecusarComoDocumentoInvalido(nomeDoValor, Marcador);
    }

    [Theory(DisplayName = "O nulo gravado onde o valor é obrigatório é recusado como documento inválido")]
    [InlineData(typeof(CertameDivulgado), nameof(CertameDivulgado.AtosDivulgados), nameof(CertameDivulgado.AtosDivulgados))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Respostas), nameof(ComprovanteInscricao.Respostas))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Derivados), nameof(ComprovanteInscricao.Derivados))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Grupos), nameof(ComprovanteInscricao.Grupos))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Aceites), nameof(ComprovanteInscricao.Aceites))]
    [InlineData(typeof(ComprovanteInscricao), nameof(ComprovanteInscricao.Documentos), nameof(ComprovanteInscricao.Documentos))]
    [InlineData(typeof(CriterioDesempate), nameof(CriterioDesempate.Parametros), nameof(ParametrosCriterioDesempate))]
    [InlineData(typeof(RegraEliminacao), nameof(RegraEliminacao.Parametros), nameof(ParametrosRegraEliminacao))]
    [InlineData(typeof(ConfiguracaoDistribuicaoVagas), nameof(ConfiguracaoDistribuicaoVagas.AjusteDoQuadro), nameof(ConfiguracaoDistribuicaoVagas.AjusteDoQuadro))]
    [InlineData(typeof(ConfiguracaoDistribuicaoVagas), nameof(ConfiguracaoDistribuicaoVagas.VagasRetiradasNoAjuste), nameof(ConfiguracaoDistribuicaoVagas.VagasRetiradasNoAjuste))]
    public void Leitura_NuloGravado_RecusaComoDocumentoInvalido(Type entidade, string propriedade, string nomeDoValor)
    {
        Action leitura = () => Conversor(entidade, propriedade).ConvertFromProvider(NuloGravado);

        leitura.DeveRecusarComoDocumentoInvalido(nomeDoValor);
    }

    [Fact(DisplayName = "O formato gravado com tamanho máximo inválido é recusado pelo código da recusa, sem o conteúdo gravado")]
    public void Leitura_FormatoComTamanhoInvalido_RecusaPeloCodigoDaRecusa()
    {
        Action leitura = () => Conversor(typeof(FormatosPermitidos), nameof(FormatosPermitidos.Lista))
            .ConvertFromProvider("""[{"formato":"PDF","tamanhoMaximoBytesMax":0}]""");

        leitura.Should().Throw<InvalidOperationException>().Which.Message.Should().Be(
            "Dado inválido no banco ao reidratar FormatoPermitidoEntry: FormatosPermitidos.TamanhoMaximoBytesMaxInvalido. " +
            "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
    }

    [Fact(DisplayName = "Os formatos gravados válidos continuam sendo lidos")]
    public void Leitura_FormatosValidos_SaoLidos()
    {
        object? lido = Conversor(typeof(FormatosPermitidos), nameof(FormatosPermitidos.Lista))
            .ConvertFromProvider("""[{"formato":"PDF","tamanhoMaximoBytesMax":1024},{"formato":"PNG","tamanhoMaximoBytesMax":null}]""");

        lido.Should().BeAssignableTo<IReadOnlyList<FormatoPermitidoEntry>>().Which.Should().HaveCount(2);
    }

    private static ValueConverter Conversor(Type entidade, string propriedade) =>
        ModeloDaSelecao.Propriedade(entidade, propriedade).GetValueConverter()!;
}
