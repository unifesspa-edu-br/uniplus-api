namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento;

using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Um processo seletivo gravado e os colaboradores reais com que o envio do modelo de documento é percorrido,
/// sem o pipeline HTTP: iniciar, enviar o conteúdo à URL pré-assinada com o tipo de mídia exigido, confirmar e
/// abrir o acesso.
/// </summary>
internal sealed class EnvioDoModeloDeDocumento : IAsyncDisposable
{
    private readonly SelecaoDbContext _context;
    private readonly ModeloDeDocumentoRepository _modelos;
    private readonly ProcessoSeletivoRepository _processos;
    private readonly IArquivoArmazenadoStorage _storage;

    private EnvioDoModeloDeDocumento(SelecaoDbContext context, ProcessoSeletivo processo, IArquivoArmazenadoStorage storage)
    {
        _context = context;
        Processo = processo;
        _modelos = new ModeloDeDocumentoRepository(context);
        _processos = new ProcessoSeletivoRepository(context, TimeProvider.System);
        _storage = storage;
    }

    public ProcessoSeletivo Processo { get; }

    public static async Task<EnvioDoModeloDeDocumento> NovoAsync(
        ProcessoSeletivoDbFixture db, IArquivoArmazenadoStorage storage, string nomeDoProcesso)
    {
        ArgumentNullException.ThrowIfNull(db);
        SelecaoDbContext context = db.CreateDbContext();
        ProcessoSeletivo processo = await ProcessoSeletivoDeTeste.GravarAsync(context, nomeDoProcesso);
        return new EnvioDoModeloDeDocumento(context, processo, storage);
    }

    public async Task<IniciarEnvioDoModeloDeDocumentoDto> IniciarEEnviarAsync(byte[] conteudo, string nomeArquivo)
    {
        Result<IniciarEnvioDoModeloDeDocumentoDto> iniciar = await IniciarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new IniciarEnvioDoModeloDeDocumentoCommand(Processo.Id, nomeArquivo, "ODT"),
            _processos, _modelos, _storage, _context, TimeProvider.System, CancellationToken.None);
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        using HttpClient http = new();
        using ByteArrayContent corpo = new(conteudo);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(iniciar.Value!.ContentTypeExigido);
        (await http.PutAsync(iniciar.Value.UrlUpload, corpo)).EnsureSuccessStatusCode();
        return iniciar.Value;
    }

    public Task<Result<ModeloDeDocumentoDto>> ConfirmarAsync(Guid modeloId) =>
        ConfirmarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new ConfirmarEnvioDoModeloDeDocumentoCommand(Processo.Id, modeloId),
            _modelos, _storage, _context, TimeProvider.System, CancellationToken.None);

    public async Task<byte[]> BaixarPeloAcessoAsync(Guid modeloId)
    {
        Result<AcessoModeloDeDocumentoDto> acesso = await ObterAcessoModeloDeDocumentoQueryHandler.Handle(
            new ObterAcessoModeloDeDocumentoQuery(Processo.Id, modeloId),
            _processos, _modelos, _storage, TimeProvider.System, CancellationToken.None);
        acesso.IsSuccess.Should().BeTrue(acesso.Error?.Message);
        using HttpClient http = new();
        return await http.GetByteArrayAsync(acesso.Value!.Url);
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
