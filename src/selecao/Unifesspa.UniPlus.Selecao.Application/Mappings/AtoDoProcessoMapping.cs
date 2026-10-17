namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Projeção <c>AtoDoProcesso</c> → <c>AtoDoProcessoDto</c>.</summary>
public static class AtoDoProcessoMapping
{
    /// <exception cref="InvalidOperationException">Documento referenciado pelo ato ausente.</exception>
    public static AtoDoProcessoDto ToDto(
        AtoDoProcesso ato,
        IReadOnlyDictionary<Guid, DocumentoAPublicar> documentos,
        bool pendenciaAcimaDoLimiar)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(documentos);

        return new AtoDoProcessoDto(
            Id: ato.Id,
            TipoCodigo: ato.TipoCodigo,
            TipoNome: ato.TipoNome,
            Numero: ato.Numero,
            DataDoAto: ato.DataDoAto,
            PublicadoEm: ato.CreatedAt,
            Assinante: ato.Assinante,
            Situacao: ato.Situacao,
            RegistradoEm: ato.RegistradoEm,
            PendenciaAcimaDoLimiar: pendenciaAcimaDoLimiar,
            AtoCorrigidoId: ato.AtoCorrigidoId,
            MotivoDaCorrecao: ato.MotivoDaCorrecao,
            ProdutoDoEditalId: ato.ProdutoDoEditalId,
            PoliticaDeDivulgacaoConfirmada: ato.PoliticaDeDivulgacaoConfirmadaEm is not null,
            DocumentoPrincipal: Documento(ato.Id, documentos, ato.DocumentoPrincipalId, nomeDeExibicao: null),
            Anexos: [.. ato.Anexos
                .OrderBy(static a => a.Ordem)
                .Select(a => Documento(ato.Id, documentos, a.DocumentoAPublicarId, a.NomeDeExibicao))]);
    }

    // O registro referencia o documento por chave estrangeira: a ausência não é um caso da consulta,
    // é dado inconsistente, e esconder o documento faria o ato parecer publicado sem ele.
    private static DocumentoDoAtoDto Documento(
        Guid atoId,
        IReadOnlyDictionary<Guid, DocumentoAPublicar> documentos,
        Guid documentoId,
        string? nomeDeExibicao) =>
        documentos.TryGetValue(documentoId, out DocumentoAPublicar? documento)
            ? Documento(documento, nomeDeExibicao)
            : throw new InvalidOperationException(
                $"O ato {atoId} referencia o documento {documentoId}, que não existe no processo.");

    private static DocumentoDoAtoDto Documento(DocumentoAPublicar documento, string? nomeDeExibicao) =>
        new(documento.Id, documento.NomeArquivo, nomeDeExibicao, documento.TipoDeMidia, documento.HashSha256);
}
