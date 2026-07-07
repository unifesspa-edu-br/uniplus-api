namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class IniciarUploadDocumentoEditalCommandHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente recusa sem criar documento")]
    public async Task Handle_ProcessoInexistente_Recusa()
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        processoRepository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProcessoSeletivo?)null);

        Result<IniciarUploadDocumentoEditalDto> resultado = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(Guid.CreateVersion7()),
            processoRepository, documentoRepository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await documentoRepository.DidNotReceive().AdicionarAsync(Arg.Any<DocumentoEdital>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com processo existente cria documento pendente e devolve URL pre-assinada")]
    public async Task Handle_ProcessoExistente_CriaPendenteEDevolveUrl()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        processoRepository.ObterPorIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        storage.GerarUrlUploadAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://minio.local/uniplus-documentos/x.pdf?X-Amz-Signature=fake");

        Result<IniciarUploadDocumentoEditalDto> resultado = await IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UrlUpload.Should().Be(new Uri("https://minio.local/uniplus-documentos/x.pdf?X-Amz-Signature=fake"));
        await documentoRepository.Received(1).AdicionarAsync(
            Arg.Is<DocumentoEdital>(d => d.ProcessoSeletivoId == processo.Id && d.Status == StatusDocumentoEdital.Pendente),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle não persiste nada quando a geração da URL pre-assinada falha")]
    public async Task Handle_FalhaAoGerarUrl_NaoPersisteRegistroOrfao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        processoRepository.ObterPorIdAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        storage.GerarUrlUploadAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("MinIO indisponível (simulado)"));

        Func<Task> act = () => IniciarUploadDocumentoEditalCommandHandler.Handle(
            new IniciarUploadDocumentoEditalCommand(processo.Id),
            processoRepository, documentoRepository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // Sem isso, uma falha no presign depois do SaveChanges deixaria uma
        // linha pendente órfã (sem URL nunca entregue ao cliente) e, sob
        // retry com a mesma Idempotency-Key, criaria outra a cada tentativa.
        await documentoRepository.DidNotReceive().AdicionarAsync(Arg.Any<DocumentoEdital>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
