namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.IO.Compression;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O modelo de documento que a exigência oferece ao candidato (UNI-REQ-0016): só documento de
/// texto editável — DOCX ou ODT, conferido pela estrutura do pacote —, sem macro, com o nome que o
/// candidato recebe no download.
/// </summary>
public sealed class ModeloDeDocumentoTests
{
    private static readonly TimeProvider Relogio = TimeProvider.System;

    [Theory(DisplayName = "Documento de texto DOCX, qualquer que seja o nome da parte principal, e ODT, com o content-type do formato, são aceitos")]
    [InlineData("DOCX", "word/document.xml")]
    [InlineData("DOCX", "word/Document2.xml")]
    [InlineData("ODT", null)]
    public void ValidarConteudo_DocumentoDeTexto_Aceita(string formato, string? parte)
    {
        ModeloDeDocumento modelo = Pendente(formato);
        byte[] conteudo = parte is null ? ArquivosDeModeloDeTeste.Odt() : ArquivosDeModeloDeTeste.Docx(parte);

        modelo.ValidarConteudo(conteudo.Length, modelo.ContentType, conteudo).IsSuccess.Should().BeTrue();
    }

    public static TheoryData<string, string, byte[], string> Recusados => new()
    {
        { "DOCX", ArquivosDeModeloDeTeste.ContentTypeDocx, ArquivosDeModeloDeTeste.Pdf(), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "DOCX", ArquivosDeModeloDeTeste.ContentTypeDocx, ArquivosDeModeloDeTeste.PlanilhaComoDocx(), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "DOCX", "application/pdf", ArquivosDeModeloDeTeste.Docx(), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "DOCX", ArquivosDeModeloDeTeste.ContentTypeDocx, ArquivosDeModeloDeTeste.DocxComMacro(), ModeloDeDocumentoErrorCodes.ContemMacro },
        { "DOCX", ArquivosDeModeloDeTeste.ContentTypeDocx, ArquivosDeModeloDeTeste.DocxComPrincipalNaoDeclarado(), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "DOCX", ArquivosDeModeloDeTeste.ContentTypeDocx, ArquivosDeModeloDeTeste.Docx(comAParte: false), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Odt(comConteudo: false), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Odt(comManifesto: false), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Odt("application/vnd.oasis.opendocument.spreadsheet"), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Odt("application/vnd.oasis.opendocument.texx"), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Odt("application/vnd.oasis.opendocument.text-template"), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Odt(compressaoDoMimetype: CompressionLevel.Optimal), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Odt(entradaExtra: "Basic/Standard/Module1.xml"), ModeloDeDocumentoErrorCodes.ContemMacro },
        { "ODT", ArquivosDeModeloDeTeste.ContentTypeOdt, ArquivosDeModeloDeTeste.Docx(), ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato },
    };

    [Theory(DisplayName = "PDF, planilha renomeada, pacote só com os metadados, content-type de outro formato, ODT de planilha, de modelo, sem conteúdo, sem manifesto ou com o mimetype comprimido e pacote com macro são recusados")]
    [MemberData(nameof(Recusados))]
    public void ValidarConteudo_ForaDoFormato_Recusa(string formato, string contentType, byte[] conteudo, string codigo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ModeloDeDocumento modelo = Pendente(formato);

        modelo.ValidarConteudo(conteudo.Length, contentType, conteudo).Error!.Code.Should().Be(codigo);
    }

    [Fact(DisplayName = "O arquivo acima de 10 MB é recusado")]
    public void ValidarConteudo_TamanhoExcedido_Recusa()
    {
        ModeloDeDocumento modelo = Pendente("DOCX");
        byte[] conteudo = ArquivosDeModeloDeTeste.Docx();

        modelo.ValidarConteudo(ModeloDeDocumento.TamanhoMaximoBytes + 1, modelo.ContentType, conteudo).Error!.Code
            .Should().Be(ModeloDeDocumentoErrorCodes.TamanhoExcedido);
    }

    [Theory(DisplayName = "O nome do arquivo ganha a extensão do formato, sem duplicá-la")]
    [InlineData("Autodeclaração", "DOCX", "Autodeclaração.docx")]
    [InlineData("  Declaração de pertencimento.ODT ", "ODT", "Declaração de pertencimento.odt")]
    public void IniciarPendente_NomeComExtensaoDoFormato(string nome, string formato, string esperado)
    {
        Pendente(formato, nome).NomeArquivo.Should().Be(esperado);
    }

    [Theory(DisplayName = "Nome vazio, com separador de caminho, caractere proibido ou acima de 120 caracteres é recusado")]
    [InlineData(" ")]
    [InlineData("../anexo")]
    [InlineData("anexo:1")]
    [InlineData("anexo\u0001")]
    public void IniciarPendente_NomeInvalido_Recusa(string nome)
    {
        Result<ModeloDeDocumento> resultado = ModeloDeDocumento.IniciarPendente(Guid.NewGuid(), nome, "DOCX", Relogio, TimeSpan.FromMinutes(15));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "nomeArquivo",
            Error = new { Code = ModeloDeDocumentoErrorCodes.NomeArquivoInvalido },
        });
    }

    [Theory(DisplayName = "O nome tem até 120 caracteres com a extensão")]
    [InlineData(115, true)]
    [InlineData(116, false)]
    public void IniciarPendente_TamanhoDoNome(int tamanhoSemExtensao, bool aceita)
    {
        ModeloDeDocumento.IniciarPendente(Guid.NewGuid(), new string('a', tamanhoSemExtensao), "DOCX", Relogio, TimeSpan.FromMinutes(15))
            .IsSuccess.Should().Be(aceita);
    }

    [Fact(DisplayName = "Formato e nome inválidos são recusados juntos")]
    public void IniciarPendente_FormatoENomeInvalidos_RecusasAcumulam()
    {
        Result<ModeloDeDocumento> resultado = ModeloDeDocumento.IniciarPendente(Guid.NewGuid(), "", "PDF", Relogio, TimeSpan.FromMinutes(15));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("formato", ModeloDeDocumentoErrorCodes.FormatoNaoEditavel),
            ("nomeArquivo", ModeloDeDocumentoErrorCodes.NomeArquivoInvalido),
        ]);
    }

    [Theory(DisplayName = "Formato que não é documento de texto editável é recusado")]
    [InlineData("PDF")]
    [InlineData("docx")]
    [InlineData(null)]
    public void IniciarPendente_FormatoNaoEditavel_Recusa(string? formato)
    {
        Result<ModeloDeDocumento> resultado = ModeloDeDocumento.IniciarPendente(Guid.NewGuid(), "Autodeclaração", formato, Relogio, TimeSpan.FromMinutes(15));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloDeDocumentoErrorCodes.FormatoNaoEditavel);
    }

    [Fact(DisplayName = "Só o modelo pendente é confirmado, e a chave selada tem a extensão do formato")]
    public void Confirmar_SoDoPendente()
    {
        ModeloDeDocumento modelo = Pendente("ODT");

        modelo.Confirmar(10, new string('a', 64), Relogio).IsSuccess.Should().BeTrue();
        modelo.ObjectKeyConfirmado.Should().EndWith("/confirmado.odt");
        modelo.Confirmar(10, new string('a', 64), Relogio).Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.StatusInvalidoParaConfirmacao);
    }

    private static ModeloDeDocumento Pendente(string formato, string nome = "Autodeclaração") =>
        ModeloDeDocumento.IniciarPendente(Guid.NewGuid(), nome, formato, Relogio, TimeSpan.FromMinutes(15)).Value!;
}
