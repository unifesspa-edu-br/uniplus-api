namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Projeção <c>AtoDoProcesso</c> → <c>AtoDoProcessoDto</c>.</summary>
public static class AtoDoProcessoMapping
{
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
            DocumentoPrincipal: documentos.TryGetValue(ato.DocumentoPrincipalId, out DocumentoAPublicar? principal)
                ? Documento(principal, nomeDeExibicao: null)
                : null,
            Anexos: [.. ato.Anexos
                .OrderBy(static a => a.Ordem)
                .Where(a => documentos.ContainsKey(a.DocumentoAPublicarId))
                .Select(a => Documento(documentos[a.DocumentoAPublicarId], a.NomeDeExibicao))]);
    }

    private static DocumentoDoAtoDto Documento(DocumentoAPublicar documento, string? nomeDeExibicao) =>
        new(documento.Id, documento.NomeArquivo, nomeDeExibicao, documento.TipoDeMidia, documento.HashSha256);
}
