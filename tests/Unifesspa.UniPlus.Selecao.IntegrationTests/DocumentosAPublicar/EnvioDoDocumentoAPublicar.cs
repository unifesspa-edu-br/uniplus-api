namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using System.Net.Http.Headers;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O dono do documento — um processo seletivo gravado, ou só o rascunho dele, sem linha de processo — e os colaboradores
/// reais com que o envio direto do documento a publicar é percorrido, sem o pipeline HTTP: iniciar, enviar o conteúdo à
/// URL pré-assinada, confirmar e pedir o acesso.
/// </summary>
internal sealed class EnvioDoDocumentoAPublicar : IAsyncDisposable
{
    private static readonly TimeSpan EsperaPeloBloqueio = TimeSpan.FromSeconds(10);

    private readonly ProcessoSeletivoRepository _processos;
    private readonly IArquivoArmazenadoStorage _storage;

    private EnvioDoDocumentoAPublicar(UniPlusDbContext context, Guid processoId, IArquivoArmazenadoStorage storage)
    {
        Context = context;
        ProcessoId = processoId;
        Documentos = new DocumentoAPublicarRepository(context);
        _processos = ProcessoSeletivoRepositoryDeTeste.Criar(context);
        _storage = storage;
    }

    public UniPlusDbContext Context { get; }

    /// <summary>A identidade do dono: a do processo gravado ou a do rascunho que ainda não virou processo.</summary>
    public Guid ProcessoId { get; }

    public DocumentoAPublicarRepository Documentos { get; }

    /// <summary>O bloqueio do dono que a confirmação adquire, com espera suficiente para aguardar quem a retém.</summary>
    public BloqueioDoDonoDoDocumentoAPublicar Bloqueio => new(
        new RascunhoDeProcessoSeletivoRepository(Context, Options.Create(new TravaDoProcessoOptions { EsperaMaxima = EsperaPeloBloqueio })),
        _processos);

    public static async Task<EnvioDoDocumentoAPublicar> NovoAsync(ProcessoSeletivoDbFixture db, IArquivoArmazenadoStorage storage)
    {
        ArgumentNullException.ThrowIfNull(db);
        UniPlusDbContext context = db.CreateDbContext();
        return new EnvioDoDocumentoAPublicar(context, (await ProcessoSeletivoDeTeste.GravarAsync(context, "PS 2026 — SiSU")).Id, storage);
    }

    /// <summary>O rascunho gravado e nenhuma linha de processo: o dono antes da primeira publicação.</summary>
    public static async Task<EnvioDoDocumentoAPublicar> NovoDeRascunhoAsync(ProcessoSeletivoDbFixture db, IArquivoArmazenadoStorage storage)
    {
        ArgumentNullException.ThrowIfNull(db);
        Guid rascunhoId = await RascunhosSemeadosParaTeste.SemearRascunhoAsync(db, Guid.CreateVersion7());
        return new EnvioDoDocumentoAPublicar(db.CreateDbContext(), rascunhoId, storage);
    }

    /// <summary>O processo excluído logicamente, que mantém a linha e os documentos mas está fora de alcance.</summary>
    public static async Task<EnvioDoDocumentoAPublicar> NovoDeProcessoExcluidoAsync(ProcessoSeletivoDbFixture db, IArquivoArmazenadoStorage storage)
    {
        ArgumentNullException.ThrowIfNull(db);
        ProcessoSeletivo processo = await RascunhosSemeadosParaTeste.SemearProcessoExcluidoAsync(db, Guid.CreateVersion7());
        return new EnvioDoDocumentoAPublicar(db.CreateDbContext(), processo.Id, storage);
    }

    /// <summary>Um identificador que não é de rascunho nem de processo.</summary>
    public static EnvioDoDocumentoAPublicar NovoSemDono(ProcessoSeletivoDbFixture db, IArquivoArmazenadoStorage storage)
    {
        ArgumentNullException.ThrowIfNull(db);
        return new EnvioDoDocumentoAPublicar(db.CreateDbContext(), Guid.CreateVersion7(), storage);
    }

    /// <summary>O mesmo dono e o mesmo armazenamento, sobre uma conexão nova: o que o banco guardou, sem o rastreamento do contexto anterior.</summary>
    public EnvioDoDocumentoAPublicar EmNovoContexto(ProcessoSeletivoDbFixture db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return new EnvioDoDocumentoAPublicar(db.CreateDbContext(), ProcessoId, _storage);
    }

    public static async Task EnviarAsync(Uri urlDeUpload, byte[] conteudo, string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf)
    {
        using HttpClient http = new();
        using ByteArrayContent corpo = new(conteudo);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(tipoDeMidia);
        using HttpResponseMessage resposta = await http.PutAsync(urlDeUpload, corpo);
        resposta.EnsureSuccessStatusCode();
    }

    public Task<Result<IniciarEnvioDoDocumentoAPublicarDto>> TentarIniciarAsync(
        FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital,
        string nomeArquivo = "edital.pdf",
        string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf) =>
        IniciarEnvioDoDocumentoAPublicarCommandHandler.Handle(
            new IniciarEnvioDoDocumentoAPublicarCommand(ProcessoId, finalidade, nomeArquivo, tipoDeMidia),
            new UnidadeDonaDoProcesso(Context), Documentos, _storage, Context, TimeProvider.System, CancellationToken.None);

    public async Task<IniciarEnvioDoDocumentoAPublicarDto> IniciarAsync(
        FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital,
        string nomeArquivo = "edital.pdf",
        string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf)
    {
        Result<IniciarEnvioDoDocumentoAPublicarDto> iniciar = await TentarIniciarAsync(finalidade, nomeArquivo, tipoDeMidia);
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
            new ConfirmarEnvioDoDocumentoAPublicarCommand(ProcessoId, documentoId),
            Bloqueio, Documentos, armazenamento ?? _storage, Context, TimeProvider.System, CancellationToken.None);

    /// <summary>
    /// A confirmação como o Wolverine a executa: numa transação ambiente, que retém o bloqueio do dono até o fim e é
    /// confirmada junto com a gravação.
    /// </summary>
    public async Task<Result<DocumentoAPublicarDto>> ConfirmarNaTransacaoAsync(Guid documentoId, IArquivoArmazenadoStorage? armazenamento = null)
    {
        await using IDbContextTransaction transacao = await Context.Database.BeginTransactionAsync();
        Result<DocumentoAPublicarDto> resultado = await ConfirmarAsync(documentoId, armazenamento);
        await transacao.CommitAsync();
        return resultado;
    }

    /// <summary>Grava o documento já confirmado, com a cópia gravada, como o banco o guarda depois da confirmação.</summary>
    public async Task<DocumentoAPublicar> GravarConfirmadoAsync()
    {
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(ProcessoId);
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        Context.DocumentosAPublicar.Add(documento);
        await Context.SaveChangesAsync();
        return documento;
    }

    public Task<Result<ListarDocumentosAPublicarResult>> ListarAsync() =>
        ListarDocumentosAPublicarQueryHandler.Handle(
            new ListarDocumentosAPublicarQuery(ProcessoId), new UnidadeDonaDoProcesso(Context), Documentos, CancellationToken.None);

    public Task<Result<AcessoDocumentoAPublicarDto>> AcessarAsync(Guid documentoId) =>
        ObterAcessoDocumentoAPublicarQueryHandler.Handle(
            new ObterAcessoDocumentoAPublicarQuery(ProcessoId, documentoId),
            new UnidadeDonaDoProcesso(Context), Documentos, _storage, TimeProvider.System, CancellationToken.None);

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}
