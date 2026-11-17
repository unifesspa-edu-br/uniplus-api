namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Security.Cryptography;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ConfirmarEnvioDoDocumentoAPublicarCommandHandlerTests
{
    private static readonly byte[] ConteudoPdf = CenarioDoDocumentoAPublicar.ConteudoPdf;

    private readonly CenarioDoDocumentoAPublicar _cenario = new CenarioDoDocumentoAPublicar().ComRascunhoComoDono();

    private async Task NadaFoiConfirmado()
    {
        _cenario.Documentos.DidNotReceiveWithAnyArgs().Atualizar(default!);
        await _cenario.Storage.DidNotReceiveWithAnyArgs().SalvarConteudoSeladoAsync(default!, default!, default!, default);
    }

    [Fact(DisplayName = "Documento inexistente recusa 404")]
    public async Task DocumentoInexistente_Recusa()
    {
        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.NaoEncontrado");
    }

    [Fact(DisplayName = "Documento de outro processo recusa 404")]
    public async Task DocumentoDeOutroProcesso_Recusa()
    {
        DocumentoAPublicar deOutroProcesso = CenarioDoDocumentoAPublicar.Pendente(Guid.CreateVersion7());
        _cenario.Documentos.ObterPorIdAsync(deOutroProcesso.Id, Arg.Any<CancellationToken>()).Returns(deOutroProcesso);

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(deOutroProcesso.Id);

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.NaoEncontrado");
    }

    [Fact(DisplayName = "Objeto ausente no armazenamento recusa 422 ObjetoNaoEncontrado")]
    public async Task ObjetoAusente_Recusa()
    {
        DocumentoAPublicar documento = _cenario.PendenteSemObjetoEnviado();

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.ObjetoNaoEncontrado");
    }

    [Fact(DisplayName = "Tamanho acima do limite recusa 422 sem baixar o conteúdo")]
    public async Task TamanhoExcedido_RecusaSemBaixarConteudo()
    {
        DocumentoAPublicar documento = _cenario.PendenteSemObjetoEnviado();
        _cenario.Storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(DocumentoAPublicar.TamanhoMaximoBytes + 1, "application/pdf"));

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.TamanhoExcedido");
        await _cenario.Storage.DidNotReceiveWithAnyArgs().AbrirLeituraAsync(default!, default, default);
    }

    [Fact(DisplayName = "Conteúdo sem a assinatura PDF recusa 422 sem reivindicar a confirmação")]
    public async Task AssinaturaInvalida_RecusaSemReivindicar()
    {
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado([.. "não é um pdf de verdade"u8]);

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.ConteudoDivergeDoFormato");
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().TentarReivindicarConfirmacaoAsync(default, default);
        await NadaFoiConfirmado();
    }

    [Fact(DisplayName = "PDF válido confirma, calcula o SHA-256, sela a cópia e grava")]
    public async Task PdfValido_ConfirmaESela()
    {
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado();

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Value!.HashSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(ConteudoPdf)));
        resultado.Value.Status.Should().Be("Confirmado");
        _cenario.Documentos.Received(1).Atualizar(documento);
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cenario.Storage.Received(1).SalvarConteudoSeladoAsync(
            documento.ObjectKeyConfirmado!, Arg.Is<byte[]>(b => b.SequenceEqual(ConteudoPdf)), TiposDeMidiaDeDocumento.Pdf, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Quem perde a reivindicação atômica recusa sem escrever no armazenamento")]
    public async Task PerdeuReivindicacao_RecusaSemEscrever()
    {
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado();
        _cenario.Documentos.TentarReivindicarConfirmacaoAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.StatusInvalidoParaConfirmacao");
        await NadaFoiConfirmado();
    }

    [Fact(DisplayName = "Entidade que já não está pendente depois da reivindicação lança, para reverter a transação")]
    public async Task EntidadeSincronizadaAposReivindicacao_Lanca()
    {
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado();
        _cenario.Documentos.TentarReivindicarConfirmacaoAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(_ => documento.Confirmar(ConteudoPdf.Length, new string('0', 64), TimeProvider.System).IsSuccess);

        Func<Task> confirmar = () => _cenario.ConfirmarAsync(documento.Id);

        await confirmar.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{documento.Id}*StatusInvalidoParaConfirmacao*");
        await _cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Conteúdo real acima do limite recusa 422 ainda que o stat relate tamanho menor")]
    public async Task ConteudoRealExcedeOLimite_Recusa()
    {
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado(new byte[DocumentoAPublicar.TamanhoMaximoBytes + 1024]);
        _cenario.Storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>()).Returns(new InfoObjetoArmazenado(10, "application/pdf"));

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.TamanhoExcedido");
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().TentarReivindicarConfirmacaoAsync(default, default);
    }

    [Fact(DisplayName = "DOCX válido confirma e sela a cópia com o tipo de mídia do formato")]
    public async Task DocxValido_SelaComOTipoDeMidiaDoFormato()
    {
        byte[] docx = ArquivosDeModeloDeTeste.Docx();
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado(docx, TiposDeMidiaDeDocumento.Docx);

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Value!.TipoDeMidia.Should().Be(TiposDeMidiaDeDocumento.Docx);
        await _cenario.Storage.Received(1).SalvarConteudoSeladoAsync(
            documento.ObjectKeyConfirmado!, Arg.Any<byte[]>(), TiposDeMidiaDeDocumento.Docx, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Objeto de 0 bytes recusa 422 sem tentar ler: o Range não é satisfazível sobre objeto vazio")]
    public async Task ObjetoVazio_RecusaSemLer()
    {
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado([]);

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Error!.Code.Should().Be("DocumentoAPublicar.ArquivoVazio");
        await _cenario.Storage.DidNotReceiveWithAnyArgs().AbrirLeituraAsync(default!, default, default);
    }
}
