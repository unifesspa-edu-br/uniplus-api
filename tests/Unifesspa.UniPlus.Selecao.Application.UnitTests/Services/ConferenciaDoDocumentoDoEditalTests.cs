namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// O documento que o ato do edital referencia — na publicação, na retificação e no fechamento da
/// retificação — passa pela mesma conferência: existe, é do processo, está confirmado e foi enviado
/// como edital.
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

    [Theory(DisplayName = "Documento enviado para outra finalidade é recusado como edital, mesmo sendo PDF")]
    [InlineData(FinalidadeDoDocumento.AnexoDoEdital)]
    [InlineData(FinalidadeDoDocumento.DocumentoDeAto)]
    public async Task Confere_OutraFinalidade_Recusa(FinalidadeDoDocumento finalidade)
    {
        Result<DocumentoAPublicar> resultado = await ConferirAsync(
            Documento(TiposDeMidiaDeDocumento.Pdf, confirmado: true, finalidade), ProcessoId);

        resultado.Error!.Code.Should().Be(ConferenciaDoDocumentoDoEdital.FinalidadeIncompativel);
    }

    private static DocumentoAPublicar Documento(string tipoDeMidia, bool confirmado, FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoId, finalidade, "edital", tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
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
