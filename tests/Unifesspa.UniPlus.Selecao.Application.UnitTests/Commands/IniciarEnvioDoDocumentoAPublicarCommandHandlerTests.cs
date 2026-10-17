namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

public sealed class IniciarEnvioDoDocumentoAPublicarCommandHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente recusa sem criar documento")]
    public async Task Handle_ProcessoInexistente_Recusa()
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoAPublicarRepository documentoRepository = Substitute.For<IDocumentoAPublicarRepository>();
        IArquivoArmazenadoStorage storage = Substitute.For<IArquivoArmazenadoStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        processoRepository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProcessoSeletivo?)null);

        Result<IniciarEnvioDoDocumentoAPublicarDto> resultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(Guid.CreateVersion7(), FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await documentoRepository.DidNotReceive().AdicionarAsync(Arg.Any<DocumentoAPublicar>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Handle com processo existente cria o pendente, devolve a URL pre-assinada e exige o tipo de mídia do formato")]
    [InlineData(TiposDeMidiaDeDocumento.Pdf)]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public async Task Handle_ProcessoExistente_CriaPendenteEDevolveUrl(string tipoDeMidia)
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoAPublicarRepository documentoRepository = Substitute.For<IDocumentoAPublicarRepository>();
        IArquivoArmazenadoStorage storage = Substitute.For<IArquivoArmazenadoStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        processoRepository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        storage.GerarUrlUploadAsync(Arg.Any<string>(), tipoDeMidia, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://minio.local/uniplus-documentos/x?X-Amz-Signature=fake");

        Result<IniciarEnvioDoDocumentoAPublicarDto> resultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processoId, FinalidadeDoDocumento.AnexoDoEdital, "Anexo I", tipoDeMidia),
            processoRepository, documentoRepository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UrlUpload.Should().Be(new Uri("https://minio.local/uniplus-documentos/x?X-Amz-Signature=fake"));
        resultado.Value.ContentTypeExigido.Should().Be(tipoDeMidia);
        await documentoRepository.Received(1).AdicionarAsync(
            Arg.Is<DocumentoAPublicar>(d => d.ProcessoSeletivoId == processoId && d.Status == StatusArquivoEnviado.Pendente),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa o tipo de mídia fora da lista sem gerar URL nem criar documento")]
    public async Task Handle_TipoDeMidiaForaDaLista_RecusaSemGerarUrl()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoAPublicarRepository documentoRepository = Substitute.For<IDocumentoAPublicarRepository>();
        IArquivoArmazenadoStorage storage = Substitute.For<IArquivoArmazenadoStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        processoRepository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);

        Result<IniciarEnvioDoDocumentoAPublicarDto> resultado = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "Planilha", "application/vnd.ms-excel"),
            processoRepository, documentoRepository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("DocumentoAPublicar.TipoDeMidiaNaoAceito");
        await storage.DidNotReceive().GerarUrlUploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await documentoRepository.DidNotReceive().AdicionarAsync(Arg.Any<DocumentoAPublicar>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle não persiste nada quando a geração da URL pre-assinada falha")]
    public async Task Handle_FalhaAoGerarUrl_NaoPersisteRegistroOrfao()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoAPublicarRepository documentoRepository = Substitute.For<IDocumentoAPublicarRepository>();
        IArquivoArmazenadoStorage storage = Substitute.For<IArquivoArmazenadoStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        processoRepository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        storage.GerarUrlUploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("MinIO indisponível (simulado)"));

        Func<Task> act = () => IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf),
            processoRepository, documentoRepository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // Sem isso, uma falha no presign depois do SaveChanges deixaria uma linha pendente órfã
        // (sem URL nunca entregue ao cliente) e, sob retry com a mesma Idempotency-Key, criaria
        // outra a cada tentativa.
        await documentoRepository.DidNotReceive().AdicionarAsync(Arg.Any<DocumentoAPublicar>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
