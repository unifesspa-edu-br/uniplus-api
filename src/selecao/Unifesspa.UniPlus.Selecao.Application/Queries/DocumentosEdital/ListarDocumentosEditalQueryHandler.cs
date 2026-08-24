namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosEdital;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based da leitura dos documentos do Edital. Distingue
/// processo sem documento (200 com lista vazia) de processo inexistente (404) —
/// devolver lista vazia nos dois casos diria "ainda não enviaram nada" para
/// quem, na verdade, digitou um identificador errado.
/// </summary>
public static class ListarDocumentosEditalQueryHandler
{
    public static async Task<Result<ListarDocumentosEditalResult>> Handle(
        ListarDocumentosEditalQuery query,
        IDocumentoEditalRepository documentoEditalRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(documentoEditalRepository);
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
            return Result<ListarDocumentosEditalResult>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        IReadOnlyList<DocumentoEdital> documentos = await documentoEditalRepository
            .ListarPorProcessoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        DocumentoEditalDto[] items = [.. documentos.Select(DocumentoEditalMapping.ToDto)];

        return Result<ListarDocumentosEditalResult>.Success(new ListarDocumentosEditalResult(items));
    }
}
