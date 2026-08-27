namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosEdital;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class ObterAcessoDocumentoEditalQueryHandlerTests
{
    private const string UrlAssinada = "http://minio.local/uniplus/selecao/documentos-edital/x/confirmado.pdf?X-Amz-Signature=abc";

    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static RelogioFixo Relogio() => new(Agora);

    private static DocumentoEdital Pendente(Guid processoSeletivoId, TimeProvider clock) =>
        DocumentoEdital.IniciarPendente(processoSeletivoId, clock, TimeSpan.FromMinutes(15));

    private static DocumentoEdital Confirmado(Guid processoSeletivoId, TimeProvider clock)
    {
        DocumentoEdital documento = Pendente(processoSeletivoId, clock);
        documento.Confirmar(2048, new string('a', 64), clock).IsSuccess.Should().BeTrue();
        return documento;
    }

    [Fact(DisplayName = "Processo inexistente recusa antes de tocar no documento ou no storage")]
    public async Task ProcessoInexistente_Recusa()
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        processos.ExisteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        Result<AcessoDocumentoEditalDto> resultado = await ObterAcessoDocumentoEditalQueryHandler.Handle(
            new ObterAcessoDocumentoEditalQuery(Guid.CreateVersion7(), Guid.CreateVersion7()),
            processos, documentos, storage, Relogio(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await documentos.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await storage.DidNotReceive().GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Documento inexistente recusa sem assinar")]
    public async Task DocumentoInexistente_Recusa()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        processos.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        documentos.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DocumentoEdital?)null);

        Result<AcessoDocumentoEditalDto> resultado = await ObterAcessoDocumentoEditalQueryHandler.Handle(
            new ObterAcessoDocumentoEditalQuery(processoId, Guid.CreateVersion7()),
            processos, documentos, storage, Relogio(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.NaoEncontrado");
        await storage.DidNotReceive().GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A recusa é a mesma de documento inexistente, e de propósito: distinguir
    /// as duas confirmaria a quem tenta um id colhido em outro lugar que ele
    /// existe em algum processo.
    /// </summary>
    [Fact(DisplayName = "Documento de outro processo recusa como inexistente, sem assinar")]
    public async Task DocumentoDeOutroProcesso_RecusaComoInexistente()
    {
        Guid processoId = Guid.CreateVersion7();
        RelogioFixo clock = Relogio();
        DocumentoEdital deOutroProcesso = Confirmado(Guid.CreateVersion7(), clock);

        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        processos.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        documentos.ObterPorIdAsync(deOutroProcesso.Id, Arg.Any<CancellationToken>()).Returns(deOutroProcesso);

        Result<AcessoDocumentoEditalDto> resultado = await ObterAcessoDocumentoEditalQueryHandler.Handle(
            new ObterAcessoDocumentoEditalQuery(processoId, deOutroProcesso.Id),
            processos, documentos, storage, clock, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.NaoEncontrado");
        await storage.DidNotReceive().GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O pendente não passou pela validação de conteúdo — o objeto na chave de
    /// upload pode não ser o PDF que o registro virá a atestar.
    /// </summary>
    [Fact(DisplayName = "Documento pendente recusa sem assinar")]
    public async Task DocumentoPendente_RecusaSemAssinar()
    {
        Guid processoId = Guid.CreateVersion7();
        RelogioFixo clock = Relogio();
        DocumentoEdital pendente = Pendente(processoId, clock);

        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        processos.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        documentos.ObterPorIdAsync(pendente.Id, Arg.Any<CancellationToken>()).Returns(pendente);

        Result<AcessoDocumentoEditalDto> resultado = await ObterAcessoDocumentoEditalQueryHandler.Handle(
            new ObterAcessoDocumentoEditalQuery(processoId, pendente.Id),
            processos, documentos, storage, clock, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoEdital.NaoConfirmado");
        await storage.DidNotReceive().GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Documento confirmado devolve URL assinada da cópia selada com o prazo do relógio")]
    public async Task DocumentoConfirmado_DevolveUrlDaCopiaSelada()
    {
        Guid processoId = Guid.CreateVersion7();
        RelogioFixo clock = Relogio();
        DocumentoEdital confirmado = Confirmado(processoId, clock);

        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IDocumentoEditalRepository documentos = Substitute.For<IDocumentoEditalRepository>();
        IDocumentoEditalStorage storage = Substitute.For<IDocumentoEditalStorage>();
        processos.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        documentos.ObterPorIdAsync(confirmado.Id, Arg.Any<CancellationToken>()).Returns(confirmado);
        storage.GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(UrlAssinada);

        Result<AcessoDocumentoEditalDto> resultado = await ObterAcessoDocumentoEditalQueryHandler.Handle(
            new ObterAcessoDocumentoEditalQuery(processoId, confirmado.Id),
            processos, documentos, storage, clock, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Url.Should().Be(new Uri(UrlAssinada, UriKind.Absolute));
        resultado.Value.ExpiraEm.Should().Be(Agora.Add(ObterAcessoDocumentoEditalQueryHandler.TtlLeitura));

        // A cópia selada, nunca a chave de upload: é ela que o hash atesta, e a
        // de upload segue alcançável pela URL de PUT enquanto o TTL não expira.
        await storage.Received(1).GerarUrlLeituraAsync(
            confirmado.ObjectKeyConfirmado!,
            ObterAcessoDocumentoEditalQueryHandler.TtlLeitura,
            Arg.Any<CancellationToken>());
        await storage.DidNotReceive().GerarUrlLeituraAsync(
            confirmado.ObjectKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
