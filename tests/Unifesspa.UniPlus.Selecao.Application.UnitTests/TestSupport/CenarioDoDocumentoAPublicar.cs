namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// O cenário dos handlers do documento a publicar: as portas como doubles, o dono (rascunho ou processo) escolhido pelo
/// teste e o documento pendente do processo do cenário.
/// </summary>
internal sealed class CenarioDoDocumentoAPublicar
{
    public static readonly byte[] ConteudoPdf = [.. "%PDF-1.7 conteúdo qualquer"u8];

    public Guid ProcessoId { get; } = Guid.CreateVersion7();

    public IUnidadeDonaDoProcesso Donos { get; } = Substitute.For<IUnidadeDonaDoProcesso>();

    public IRascunhoDeProcessoSeletivoRepository Rascunhos { get; } = Substitute.For<IRascunhoDeProcessoSeletivoRepository>();

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();

    public IDocumentoAPublicarRepository Documentos { get; } = Substitute.For<IDocumentoAPublicarRepository>();

    public IArquivoArmazenadoStorage Storage { get; } = Substitute.For<IArquivoArmazenadoStorage>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public BloqueioDoDonoDoDocumentoAPublicar Bloqueio => new(Rascunhos, Processos);

    public CenarioDoDocumentoAPublicar ComRascunhoComoDono()
    {
        Donos.DoProcessoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        Rascunhos.BloquearCompartilhadoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(true);
        return this;
    }

    public CenarioDoDocumentoAPublicar ComProcessoPublicadoComoDono()
    {
        Donos.DoProcessoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        Processos.ExisteAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(true);
        return this;
    }

    public static DocumentoAPublicar Pendente(Guid processoId, string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf) =>
        DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.AnexoDoEdital, "Anexo I", tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;

    /// <summary>O documento pendente do processo do cenário, já gravado, sem objeto no armazenamento.</summary>
    public DocumentoAPublicar PendenteSemObjetoEnviado(string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf)
    {
        DocumentoAPublicar documento = Pendente(ProcessoId, tipoDeMidia);
        Documentos.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        Documentos.TentarReivindicarConfirmacaoAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(true);
        return documento;
    }

    /// <summary>O documento pendente já gravado, com o objeto enviado ao armazenamento.</summary>
    public DocumentoAPublicar PendenteComObjetoEnviado(byte[]? conteudo = null, string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf)
    {
        DocumentoAPublicar documento = PendenteSemObjetoEnviado(tipoDeMidia);
        ObjetoEnviado(documento.ObjectKey, conteudo ?? ConteudoPdf, tipoDeMidia);
        return documento;
    }

    public void ObjetoEnviado(string chave, byte[] conteudo, string tipoDeMidia)
    {
        Storage.ObterInfoAsync(chave, Arg.Any<CancellationToken>()).Returns(new InfoObjetoArmazenado(conteudo.Length, tipoDeMidia));
        Storage.AbrirLeituraAsync(chave, Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => Abrir(conteudo));
    }

    public Task<Result<DocumentoAPublicarDto>> ConfirmarAsync(Guid documentoId) =>
        ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(ProcessoId, documentoId),
            Bloqueio, Documentos, Storage, UnitOfWork, TimeProvider.System, CancellationToken.None);

    public Task<Result<IniciarEnvioDoDocumentoAPublicarDto>> IniciarAsync(string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf) =>
        IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(ProcessoId, FinalidadeDoDocumento.AnexoDoEdital.Codigo(), "Anexo I", tipoDeMidia),
            Donos, Documentos, Storage, UnitOfWork, TimeProvider.System, CancellationToken.None);

    private static Task<Stream> Abrir(byte[] conteudo) => Task.FromResult<Stream>(new MemoryStream(conteudo));
}
