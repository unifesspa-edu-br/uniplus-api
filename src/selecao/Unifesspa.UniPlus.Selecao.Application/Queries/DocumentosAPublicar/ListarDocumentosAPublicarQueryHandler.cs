namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based da leitura dos documentos a publicar de um dono — o processo ou o rascunho dele. Distingue
/// dono sem documento (200 com lista vazia) de dono inexistente (404): devolver lista vazia nos dois casos diria "ainda
/// não enviaram nada" para quem, na verdade, digitou um identificador errado.
/// </summary>
public static class ListarDocumentosAPublicarQueryHandler
{
    public static async Task<Result<ListarDocumentosAPublicarResult>> Handle(
        ListarDocumentosAPublicarQuery query,
        IUnidadeDonaDoProcesso donos,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(donos);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);

        // O dono é conferido antes de qualquer documento ser lido, e não deduzido de haver documento. A porta lê pelo
        // filtro global: o processo excluído logicamente não tem dono e é recusado como inexistente, mesmo com a linha
        // e os documentos preservados. O documento de rascunho é apagado de fato com ele, e não sobra para ler.
        return await donos.DoProcessoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) is null
            ? Result<ListarDocumentosAPublicarResult>.Failure(AcessoAoProcesso.NaoEncontrado(query.ProcessoSeletivoId))
            : await ListarAsync(query, documentoAPublicarRepository, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ListarDocumentosAPublicarResult>> ListarAsync(
        ListarDocumentosAPublicarQuery query,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentoAPublicar> documentos = await documentoAPublicarRepository
            .ListarPorProcessoAsync(query.ProcessoSeletivoId, query.Finalidade, cancellationToken)
            .ConfigureAwait(false);

        DocumentoAPublicarDto[] items = [.. documentos.Select(DocumentoAPublicarMapping.ToDto)];

        return Result<ListarDocumentosAPublicarResult>.Success(new ListarDocumentosAPublicarResult(items));
    }
}
