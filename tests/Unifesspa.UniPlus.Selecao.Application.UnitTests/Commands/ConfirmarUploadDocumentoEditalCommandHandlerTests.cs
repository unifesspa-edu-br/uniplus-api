namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class ConfirmarUploadDocumentoEditalCommandHandlerTests
{
    private static readonly byte[] ConteudoPdfValido = [.. "%PDF-1.7 conteúdo qualquer"u8];

    private static (DocumentoEdital Documento, Guid ProcessoSeletivoId) NovoDocumentoPendente()
    {
        Guid processoSeletivoId = Guid.CreateVersion7();
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoSeletivoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        return (documento, processoSeletivoId);
    }

    [Fact(DisplayName = "Handle com documento inexistente recusa (404)")]
    public async Task Handle_DocumentoInexistente_Recusa()
    {
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DocumentoEdital?)null);

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(Guid.CreateVersion7(), Guid.CreateVersion7()),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com documento de outro processo recusa (404)")]
    public async Task Handle_DocumentoDeOutroProcesso_Recusa()
    {
        (DocumentoEdital documento, _) = NovoDocumentoPendente();
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(Guid.CreateVersion7(), documento.Id),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com objeto ausente no storage recusa (422 ObjetoNaoEncontrado)")]
    public async Task Handle_ObjetoAusente_Recusa()
    {
        (DocumentoEdital documento, Guid processoId) = NovoDocumentoPendente();
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>()).Returns((InfoObjetoArmazenado?)null);

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processoId, documento.Id),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.ObjetoNaoEncontrado");
    }

    [Fact(DisplayName = "Handle recusa por tamanho sem baixar o conteúdo (422 TamanhoExcedido)")]
    public async Task Handle_TamanhoExcedido_RecusaSemBaixarConteudo()
    {
        (DocumentoEdital documento, Guid processoId) = NovoDocumentoPendente();
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(DocumentoEdital.TamanhoMaximoBytes + 1, "application/pdf"));

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processoId, documento.Id),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.TamanhoExcedido");
        await storage.DidNotReceive().AbrirLeituraAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa conteúdo sem assinatura PDV válida (422 AssinaturaInvalida)")]
    [SuppressMessage(
        "Reliability",
        "CA2025:Object MemoryStream can be disposed of before operation completes",
        Justification = "O stream é consumido de forma síncrona dentro do await Handle(...) — a leitura pelo handler termina antes do using declarado sair de escopo no fim do método.")]
    public async Task Handle_AssinaturaInvalida_Recusa()
    {
        (DocumentoEdital documento, Guid processoId) = NovoDocumentoPendente();
        byte[] conteudoFalso = [.. "não é um pdf de verdade"u8];
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(conteudoFalso.Length, "application/pdf"));
        using MemoryStream streamConteudoFalso = new(conteudoFalso);
        storage.AbrirLeituraAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(streamConteudoFalso));

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processoId, documento.Id),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.AssinaturaInvalida");
        repository.DidNotReceive().Atualizar(Arg.Any<DocumentoEdital>());
        await storage.DidNotReceive().SalvarConteudoSeladoAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        // Conteúdo inválido nunca chega a reivindicar a confirmação — senão o
        // registro travaria como Confirmado sem hash caso a validação recuse.
        await repository.DidNotReceive().TentarReivindicarConfirmacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com PDF válido confirma, calcula sha256 e persiste (fluxo feliz)")]
    [SuppressMessage(
        "Reliability",
        "CA2025:Object MemoryStream can be disposed of before operation completes",
        Justification = "O stream é consumido de forma síncrona dentro do await Handle(...) — a leitura pelo handler termina antes do using declarado sair de escopo no fim do método.")]
    public async Task Handle_PdfValido_ConfirmaEPersiste()
    {
        (DocumentoEdital documento, Guid processoId) = NovoDocumentoPendente();
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        repository.TentarReivindicarConfirmacaoAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(true);
        storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(ConteudoPdfValido.Length, "application/pdf"));
        using MemoryStream streamConteudoValido = new(ConteudoPdfValido);
        storage.AbrirLeituraAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(streamConteudoValido));
        string hashEsperado = Convert.ToHexStringLower(SHA256.HashData(ConteudoPdfValido));

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processoId, documento.Id),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.HashSha256.Should().Be(hashEsperado);
        resultado.Value.TamanhoBytes.Should().Be(ConteudoPdfValido.Length);
        resultado.Value.Status.Should().Be("Confirmado");
        repository.Received(1).Atualizar(documento);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        // A cópia selada é o que efetivamente torna o documento imutável — a
        // URL de upload original ainda aponta para ObjectKey, sobrescrevível
        // até o TTL expirar.
        await storage.Received(1).SalvarConteudoSeladoAsync(
            documento.ObjectKeyConfirmado!, Arg.Is<byte[]>(b => b.SequenceEqual(ConteudoPdfValido)), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa quando perde a reivindicação atômica sem escrever no storage")]
    [SuppressMessage(
        "Reliability",
        "CA2025:Object MemoryStream can be disposed of before operation completes",
        Justification = "O stream é consumido de forma síncrona dentro do await Handle(...) — a leitura pelo handler termina antes do using declarado sair de escopo no fim do método.")]
    public async Task Handle_PerdeuReivindicacao_RecusaSemEscreverStorage()
    {
        (DocumentoEdital documento, Guid processoId) = NovoDocumentoPendente();
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        // Simula outra confirmação concorrente já tendo vencido a reivindicação.
        repository.TentarReivindicarConfirmacaoAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(false);
        storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(ConteudoPdfValido.Length, "application/pdf"));
        using MemoryStream streamConteudoValido = new(ConteudoPdfValido);
        storage.AbrirLeituraAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(streamConteudoValido));

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processoId, documento.Id),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.StatusInvalidoParaConfirmacao");
        await storage.DidNotReceive().SalvarConteudoSeladoAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        repository.DidNotReceive().Atualizar(Arg.Any<DocumentoEdital>());
    }

    [Fact(DisplayName = "Handle recusa por tamanho quando o conteúdo real excede o limite mesmo com stat menor (TOCTOU)")]
    [SuppressMessage(
        "Reliability",
        "CA2025:Object MemoryStream can be disposed of before operation completes",
        Justification = "O stream é consumido de forma síncrona dentro do await Handle(...) — a leitura pelo handler termina antes do using declarado sair de escopo no fim do método.")]
    public async Task Handle_ConteudoRealExcedeLimiteApesarDoStatMenor_Recusa()
    {
        (DocumentoEdital documento, Guid processoId) = NovoDocumentoPendente();
        IDocumentoEditalRepository repository = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        // Stat relata um tamanho pequeno — simula o objeto de staging tendo
        // sido substituído por um maior entre o stat e a leitura (a chave
        // original segue sobrescrevível até o TTL expirar).
        storage.ObterInfoAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(10, "application/pdf"));
        byte[] conteudoReal = new byte[DocumentoEdital.TamanhoMaximoBytes + 1024];
        using MemoryStream streamReal = new(conteudoReal);
        storage.AbrirLeituraAsync(documento.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(streamReal));

        Result<DocumentoEditalDto> resultado = await ConfirmarUploadDocumentoEditalCommandHandler.Handle(
            new ConfirmarUploadDocumentoEditalCommand(processoId, documento.Id),
            repository, storage, unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.TamanhoExcedido");
        await repository.DidNotReceive().TentarReivindicarConfirmacaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
