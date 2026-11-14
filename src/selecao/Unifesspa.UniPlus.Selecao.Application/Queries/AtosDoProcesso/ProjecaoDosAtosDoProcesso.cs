namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Projeta registros de atos do processo no DTO da consulta administrativa, buscando só os documentos que os
/// atos referenciam. É o que a lista e a consulta de um ato têm em comum.
/// </summary>
internal static class ProjecaoDosAtosDoProcesso
{
    /// <remarks>A ordem do resultado é a de <paramref name="atos"/>.</remarks>
    internal static async Task<AtoDoProcessoDto[]> ProjetarAsync(
        Guid processoSeletivoId,
        IReadOnlyList<AtoDoProcesso> atos,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken)
    {
        // O histórico de envios do processo inclui documentos que nenhum ato usou.
        Guid[] idsDosDocumentos =
        [
            .. atos.SelectMany(static a => a.Anexos.Select(static x => x.DocumentoAPublicarId).Append(a.DocumentoPrincipalId)).Distinct(),
        ];
        IReadOnlyList<DocumentoAPublicar> documentos = await documentoAPublicarRepository
            .ListarDoProcessoAsync(processoSeletivoId, idsDosDocumentos, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, DocumentoAPublicar> documentosPorId = documentos.ToDictionary(static d => d.Id);

        return [.. atos.Select(a => AtoDoProcessoMapping.ToDto(a, documentosPorId))];
    }

    internal static async Task<AtoDoProcessoDto> ProjetarUmAsync(
        Guid processoSeletivoId,
        AtoDoProcesso ato,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        CancellationToken cancellationToken) =>
        (await ProjetarAsync(processoSeletivoId, [ato], documentoAPublicarRepository, cancellationToken).ConfigureAwait(false))[0];
}
