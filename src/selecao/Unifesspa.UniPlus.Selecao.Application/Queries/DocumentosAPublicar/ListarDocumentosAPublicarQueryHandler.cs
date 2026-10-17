namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based da leitura dos documentos a publicar de um processo. Distingue
/// processo sem documento (200 com lista vazia) de processo inexistente (404) —
/// devolver lista vazia nos dois casos diria "ainda não enviaram nada" para
/// quem, na verdade, digitou um identificador errado.
/// </summary>
public static class ListarDocumentosAPublicarQueryHandler
{
    public static async Task<Result<ListarDocumentosAPublicarResult>> Handle(
        ListarDocumentosAPublicarQuery query,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        // A visibilidade do processo é decidida antes de qualquer documento ser
        // lido, e não apenas quando a lista volta vazia. O processo é excluído
        // logicamente, e a exclusão preserva a linha e a chave estrangeira: um
        // processo fora de alcance continua tendo documentos apontando para
        // ele. Deduzir a existência do processo a partir de haver documento
        // devolveria 200 com a lista de um processo que todo o resto da API
        // trata como inexistente.
        bool existe = await processoSeletivoRepository
            .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (!existe)
        {
            return Result<ListarDocumentosAPublicarResult>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        IReadOnlyList<DocumentoAPublicar> documentos = await documentoAPublicarRepository
            .ListarPorProcessoAsync(query.ProcessoSeletivoId, query.Finalidade, cancellationToken)
            .ConfigureAwait(false);

        DocumentoAPublicarDto[] items = [.. documentos.Select(DocumentoAPublicarMapping.ToDto)];

        return Result<ListarDocumentosAPublicarResult>.Success(new ListarDocumentosAPublicarResult(items));
    }
}
