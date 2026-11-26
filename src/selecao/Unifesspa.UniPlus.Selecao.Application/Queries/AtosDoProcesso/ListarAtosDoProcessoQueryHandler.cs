namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lista os atos do processo, do mais recente para o mais antigo, com os documentos.
/// </summary>
/// <remarks>
/// A fonte é só o registro do ato do processo, que o edital de abertura e o de retificação também
/// têm: tipo, número, data e assinante de todos vêm do mesmo lugar.
/// </remarks>
public static class ListarAtosDoProcessoQueryHandler
{
    public static async Task<Result<ListarAtosDoProcessoResult>> Handle(
        ListarAtosDoProcessoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        bool existe = await processoSeletivoRepository
            .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (!existe)
        {
            return Result<ListarAtosDoProcessoResult>.Failure(AcessoAoProcesso.NaoEncontrado());
        }
        AtoDoProcessoDto[] itens = await AtosComDocumentosAsync(
            query.ProcessoSeletivoId, atoDoProcessoRepository, documentoAPublicarRepository, cancellationToken).ConfigureAwait(false);
        return Result<ListarAtosDoProcessoResult>.Success(new ListarAtosDoProcessoResult(itens));
    }

    /// <summary>
    /// Lista os atos na ordem do repositório (instante de gravação decrescente, ato como desempate) e os projeta com os
    /// documentos. Reordenar aqui desfaria o que o contrato do repositório promete.
    /// </summary>
    private static async Task<AtoDoProcessoDto[]> AtosComDocumentosAsync(
        Guid processoId,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AtoDoProcesso> atos = await atoDoProcessoRepository
            .ListarDoProcessoAsync(processoId, cancellationToken)
            .ConfigureAwait(false);

        return await ProjecaoDosAtosDoProcesso
            .ProjetarAsync(processoId, atos, documentoAPublicarRepository, cancellationToken)
            .ConfigureAwait(false);
    }
}
