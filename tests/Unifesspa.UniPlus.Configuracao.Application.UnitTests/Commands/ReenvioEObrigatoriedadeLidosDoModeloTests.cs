namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura do indeferimento do documento e do grupo do modelo: o que o modelo não diz sobre ser
/// obrigatório e sobre abrir prazo para reenvio é recusado, e falso dito é aceito.
/// </summary>
public sealed class ReenvioEObrigatoriedadeLidosDoModeloTests
{
    private const string Documento0 = "conjuntoDeDocumentos.documentosExigidos[0].indeferimento";
    private const string Grupo0 = "conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento";
    private const string MensagemDoObrigatorio = "Informe se o documento é obrigatório.";
    private const string MensagemDoReenvio = "Informe se o indeferimento abre prazo para reenvio.";

    private static ConteudoLido Ler(ConjuntoDeDocumentosDoModeloInput conjunto, out LeituraDoConteudo leitura)
    {
        leitura = new LeituraDoConteudo(new Dictionary<string, string>(StringComparer.Ordinal));
        return leitura.Ler(new ConteudoDoModeloInput(null, null, null, null, null, null, conjunto));
    }

    private static DocumentoExigidoDoModeloInput DocumentoCom(IndeferimentoDoDocumentoDoModeloInput? indeferimento) =>
        new("INSCRICAO", "RG", null, indeferimento!);

    private static IndeferimentoDoDocumentoDoModeloInput Indeferimento(bool? obrigatorio, bool? abrePrazo) =>
        new(obrigatorio, "RETIRA_O_ACRESCIMO", [], abrePrazo);

    private static GrupoDeDocumentosDoModeloInput GrupoCom(bool? abrePrazo, params DocumentoExigidoDoModeloInput[] documentos) =>
        new(1, new IndeferimentoDoGrupoDeDocumentosDoModeloInput("RETIRA_O_ACRESCIMO", [], abrePrazo), documentos);

    private static IReadOnlyList<(string? Campo, string Codigo, string Mensagem)> RecusasDoDocumento(IndeferimentoDoDocumentoDoModeloInput? indeferimento)
    {
        Ler(ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(DocumentoCom(indeferimento)), out LeituraDoConteudo leitura);
        return [.. leitura.Recusas.Select(static e => (e.Field, e.Error.Code, e.Error.Message))];
    }

    [Fact(DisplayName = "O documento sem dizer se é obrigatório é recusado no campo obrigatorio do indeferimento")]
    public void Ler_DocumentoSemObrigatorio_Recusa() =>
        RecusasDoDocumento(Indeferimento(null, false)).Should().Equal(
            ($"{Documento0}.obrigatorio", ModeloFormularioErrorCodes.EntradaMalformada, MensagemDoObrigatorio));

    [Fact(DisplayName = "O documento sem dizer se abre prazo para reenvio é recusado no campo abrePrazoParaReenvio do indeferimento")]
    public void Ler_DocumentoSemReenvio_Recusa() =>
        RecusasDoDocumento(Indeferimento(true, null)).Should().Equal(
            ($"{Documento0}.abrePrazoParaReenvio", ModeloFormularioErrorCodes.EntradaMalformada, MensagemDoReenvio));

    [Fact(DisplayName = "O documento sem o bloco de indeferimento é recusado nos dois campos, na ordem do bloco")]
    public void Ler_DocumentoSemIndeferimento_RecusaOsDoisCampos() =>
        RecusasDoDocumento(null).Select(static r => r.Campo).Should().Equal($"{Documento0}.obrigatorio", $"{Documento0}.abrePrazoParaReenvio");

    [Fact(DisplayName = "O documento com falso dito nos dois campos é lido sem recusa e guarda os valores")]
    public void Ler_DocumentoComFalsoDito_LeSemRecusa()
    {
        ConteudoLido lido = Ler(ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(DocumentoCom(Indeferimento(false, false))), out LeituraDoConteudo leitura);

        leitura.Recusas.Should().BeEmpty();
        IndeferimentoDoDocumentoDoModelo indeferimento = lido.ParaConteudo().Documentos.DocumentosExigidos.Single().Indeferimento;
        (indeferimento.Obrigatorio, indeferimento.AbrePrazoParaReenvio).Should().Be((false, false));
    }

    [Fact(DisplayName = "O grupo com documentos e indeferimento sem dizer se abre prazo para reenvio é recusado no campo dele")]
    public void Ler_GrupoComDocumentosSemReenvio_Recusa()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoCom(Indeferimento(true, false));

        Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(GrupoCom(null, documento)).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == $"{Grupo0}.abrePrazoParaReenvio" && e.Error.Code == ModeloFormularioErrorCodes.EntradaMalformada && e.Error.Message == MensagemDoReenvio);
    }

    [Fact(DisplayName = "O grupo com documentos e falso dito é lido sem recusa e guarda o valor")]
    public void Ler_GrupoComFalsoDito_LeSemRecusa()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoCom(Indeferimento(true, false));

        ConteudoLido lido = Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(GrupoCom(false, documento)).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Should().BeEmpty();
        lido.ParaConteudo().Documentos.GruposDeDocumentos.Single().Indeferimento!.AbrePrazoParaReenvio.Should().BeFalse();
    }

    [Fact(DisplayName = "O grupo sem bloco de indeferimento continua sem recusa")]
    public void Ler_GrupoSemIndeferimento_LeSemRecusa()
    {
        DocumentoExigidoDoModeloInput documento = DocumentoCom(Indeferimento(true, false));

        Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, documento)).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O grupo sem documentos não precisa dizer se abre prazo para reenvio")]
    public void Ler_GrupoSemDocumentosSemReenvio_LeSemRecusa()
    {
        Ler(ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(GrupoCom(null)).Construir(), out LeituraDoConteudo leitura);

        leitura.Recusas.Should().BeEmpty();
    }
}
