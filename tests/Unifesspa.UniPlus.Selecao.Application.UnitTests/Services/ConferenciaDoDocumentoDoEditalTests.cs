namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// O documento que o ato do edital referencia — na publicação, na retificação e no fechamento da
/// retificação — passa pela mesma conferência: existe, é do processo, está confirmado e é PDF.
/// </summary>
public sealed class ConferenciaDoDocumentoDoEditalTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    [Fact(DisplayName = "Documento confirmado, do processo e em PDF é aceito")]
    public async Task Confere_DocumentoValido_Aceita()
    {
        DocumentoAPublicar documento = Documento(TiposDeMidiaDeDocumento.Pdf, confirmado: true);

        Result<DocumentoAPublicar> resultado = await ConferirAsync(documento, ProcessoId);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeSameAs(documento);
    }

    [Fact(DisplayName = "Documento inexistente ou de outro processo é recusado do mesmo modo")]
    public async Task Confere_InexistenteOuDeOutroProcesso_Recusa()
    {
        DocumentoAPublicar documento = Documento(TiposDeMidiaDeDocumento.Pdf, confirmado: true);

        (await ConferirAsync(null, ProcessoId)).Error!.Code.Should().Be(ConferenciaDoDocumentoDoEdital.NaoEncontrado);
        (await ConferirAsync(documento, Guid.CreateVersion7())).Error!.Code.Should().Be(ConferenciaDoDocumentoDoEdital.NaoEncontrado);
    }

    [Fact(DisplayName = "Documento pendente é recusado, com o uso na mensagem")]
    public async Task Confere_Pendente_Recusa()
    {
        Result<DocumentoAPublicar> resultado = await ConferirAsync(Documento(TiposDeMidiaDeDocumento.Pdf, confirmado: false), ProcessoId);

        resultado.Error!.Code.Should().Be(ConferenciaDoDocumentoDoEdital.NaoConfirmado);
        resultado.Error.Message.Should().Contain("na retificação");
    }

    [Theory(DisplayName = "Documento do edital que não é PDF é recusado")]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public async Task Confere_NaoPdf_Recusa(string tipoDeMidia)
    {
        Result<DocumentoAPublicar> resultado = await ConferirAsync(Documento(tipoDeMidia, confirmado: true), ProcessoId);

        resultado.Error!.Code.Should().Be(ConferenciaDoDocumentoDoEdital.NaoEPdf);
    }

    private static DocumentoAPublicar Documento(string tipoDeMidia, bool confirmado)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoId, "edital", tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        if (confirmado)
        {
            documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        }

        return documento;
    }

    private static Task<Result<DocumentoAPublicar>> ConferirAsync(DocumentoAPublicar? armazenado, Guid processoId)
    {
        IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
        Guid documentoId = armazenado?.Id ?? Guid.CreateVersion7();
        repositorio.ObterPorIdAsync(documentoId, Arg.Any<CancellationToken>()).Returns(armazenado);

        return ConferenciaDoDocumentoDoEdital.ConferirAsync(repositorio, processoId, documentoId, "na retificação", CancellationToken.None);
    }
}
