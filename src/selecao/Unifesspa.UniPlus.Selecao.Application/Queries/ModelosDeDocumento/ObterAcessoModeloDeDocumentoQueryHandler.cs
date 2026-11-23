namespace Unifesspa.UniPlus.Selecao.Application.Queries.ModelosDeDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler do acesso de leitura ao modelo de documento. Assina só depois de estabelecer que o
/// modelo existe, é deste processo e está confirmado — e assina a cópia selada, que é a que o hash
/// atesta, nunca a chave de envio.
/// </summary>
public static class ObterAcessoModeloDeDocumentoQueryHandler
{
    public static async Task<Result<AcessoModeloDeDocumentoDto>> Handle(
        ObterAcessoModeloDeDocumentoQuery query,
        IUnidadeDonaDoProcesso donos,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IArquivoArmazenadoStorage storage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(donos);
        ArgumentNullException.ThrowIfNull(modeloDeDocumentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(clock);

        if (await donos.DoProcessoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result<AcessoModeloDeDocumentoDto>.Failure(AcessoAoProcesso.NaoEncontrado(query.ProcessoSeletivoId));
        }

        return await ObterDoProcessoAsync(query, modeloDeDocumentoRepository, storage, clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<AcessoModeloDeDocumentoDto>> ObterDoProcessoAsync(
        ObterAcessoModeloDeDocumentoQuery query, IModeloDeDocumentoRepository modelos,
        IArquivoArmazenadoStorage storage, TimeProvider clock, CancellationToken cancellationToken)
    {
        // Modelo de outro processo recebe a mesma recusa do inexistente: separar as duas
        // confirmaria a existência de um id colhido em outro lugar.
        ModeloDeDocumento? modelo = await modelos
            .ObterPorIdAsync(query.ModeloDeDocumentoId, cancellationToken)
            .ConfigureAwait(false);
        if (modelo is null || modelo.ProcessoSeletivoId != query.ProcessoSeletivoId)
        {
            return Result<AcessoModeloDeDocumentoDto>.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.NaoEncontrado, "Modelo de documento não encontrado neste Processo Seletivo."));
        }

        return await AcessarConfirmadoAsync(modelo, storage, clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<AcessoModeloDeDocumentoDto>> AcessarConfirmadoAsync(
        ModeloDeDocumento modelo, IArquivoArmazenadoStorage storage, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (modelo.Situacao != StatusArquivoEnviado.Confirmado || modelo.ObjectKeyConfirmado is null)
        {
            return Result<AcessoModeloDeDocumentoDto>.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.NaoConfirmado, "Modelo de documento ainda não confirmado — não há conteúdo validado para conferir."));
        }

        DateTimeOffset expiraEm = clock.GetUtcNow().Add(PrazosDoArquivoEnviado.Leitura);
        string url = await storage
            .GerarUrlLeituraAsync(modelo.ObjectKeyConfirmado, PrazosDoArquivoEnviado.Leitura, cancellationToken)
            .ConfigureAwait(false);

        return Result<AcessoModeloDeDocumentoDto>.Success(new AcessoModeloDeDocumentoDto(new Uri(url, UriKind.Absolute), expiraEm));
    }
}
