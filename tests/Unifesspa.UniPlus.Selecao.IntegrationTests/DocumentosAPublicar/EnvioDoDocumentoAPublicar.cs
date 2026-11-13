namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Um processo seletivo gravado e os colaboradores reais com que o envio direto do documento a publicar é
/// percorrido, sem o pipeline HTTP: iniciar, enviar o conteúdo à URL pré-assinada, confirmar e pedir o acesso.
/// </summary>
internal sealed class EnvioDoDocumentoAPublicar : IAsyncDisposable
{
    private readonly ProcessoSeletivoRepository _processos;
    private readonly IArquivoArmazenadoStorage _storage;

    private EnvioDoDocumentoAPublicar(SelecaoDbContext context, ProcessoSeletivo processo, IArquivoArmazenadoStorage storage)
    {
        Context = context;
        Processo = processo;
        Documentos = new DocumentoAPublicarRepository(context);
        _processos = new ProcessoSeletivoRepository(context, TimeProvider.System);
        _storage = storage;
    }

    public SelecaoDbContext Context { get; }

    public ProcessoSeletivo Processo { get; }

    public DocumentoAPublicarRepository Documentos { get; }

    public static async Task<EnvioDoDocumentoAPublicar> NovoAsync(ProcessoSeletivoDbFixture db, IArquivoArmazenadoStorage storage)
    {
        ArgumentNullException.ThrowIfNull(db);
        SelecaoDbContext context = db.CreateDbContext();
        ProcessoSeletivo processo = await ProcessoSeletivoDeTeste.GravarAsync(context, "PS 2026 — SiSU");
        return new EnvioDoDocumentoAPublicar(context, processo, storage);
    }

    /// <summary>O mesmo processo e o mesmo armazenamento, sobre uma conexão nova: o que o banco guardou, sem o rastreamento do contexto anterior.</summary>
    public EnvioDoDocumentoAPublicar EmNovoContexto(ProcessoSeletivoDbFixture db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return new EnvioDoDocumentoAPublicar(db.CreateDbContext(), Processo, _storage);
    }

    public static async Task EnviarAsync(Uri urlDeUpload, byte[] conteudo, string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf)
    {
        using HttpClient http = new();
        using ByteArrayContent corpo = new(conteudo);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(tipoDeMidia);
        using HttpResponseMessage resposta = await http.PutAsync(urlDeUpload, corpo);
        resposta.EnsureSuccessStatusCode();
    }

    public async Task<IniciarEnvioDoDocumentoAPublicarDto> IniciarAsync(
        FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital,
        string nomeArquivo = "edital.pdf",
        string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf)
    {
        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciar = await IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(Processo.Id, finalidade, nomeArquivo, tipoDeMidia),
            _processos, Documentos, _storage, Context, TimeProvider.System, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        return iniciar.Value!;
    }

    public async Task<IniciarEnvioDoDocumentoAPublicarDto> IniciarEEnviarAsync(
        byte[] conteudo,
        string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf,
        FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital,
        string nomeArquivo = "edital.pdf")
    {
        IniciarEnvioDoDocumentoAPublicarDto iniciar = await IniciarAsync(finalidade, nomeArquivo, tipoDeMidia);
        await EnviarAsync(iniciar.UrlUpload, conteudo, tipoDeMidia);
        return iniciar;
    }

    public Task<Result<DocumentoAPublicarDto>> ConfirmarAsync(Guid documentoId, IArquivoArmazenadoStorage? armazenamento = null) =>
        ConfirmarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(Processo.Id, documentoId),
            Documentos, armazenamento ?? _storage, Context, TimeProvider.System, CancellationToken.None);

    public Task<Result<AcessoDocumentoAPublicarDto>> AcessarAsync(Guid documentoId) =>
        ObterAcessoDocumentoAPublicarQueryHandler.Handle(
            new ObterAcessoDocumentoAPublicarQuery(Processo.Id, documentoId),
            _processos, Documentos, _storage, TimeProvider.System, CancellationToken.None);

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}
