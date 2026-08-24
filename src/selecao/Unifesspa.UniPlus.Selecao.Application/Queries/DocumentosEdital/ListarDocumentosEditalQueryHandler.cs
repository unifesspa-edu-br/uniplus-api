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

        IReadOnlyList<DocumentoEdital> documentos = await documentoEditalRepository
            .ListarPorProcessoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        // A existência do processo só é consultada quando não veio documento
        // nenhum. Documento e processo são ligados por chave estrangeira: se a
        // lista trouxe alguma coisa, o processo existe, e a segunda consulta
        // não teria como responder outra coisa.
        if (documentos.Count == 0)
        {
            bool existe = await processoSeletivoRepository
                .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
                .ConfigureAwait(false);

            if (!existe)
            {
                return Result<ListarDocumentosEditalResult>.Failure(new DomainError(
                    "ProcessoSeletivo.NaoEncontrado",
                    $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
            }
        }

        DocumentoEditalDto[] items = [.. documentos.Select(DocumentoEditalMapping.ToDto)];

        return Result<ListarDocumentosEditalResult>.Success(new ListarDocumentosEditalResult(items));
    }
}
