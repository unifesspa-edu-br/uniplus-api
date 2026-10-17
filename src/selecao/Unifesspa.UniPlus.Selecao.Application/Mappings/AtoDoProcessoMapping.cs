namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Projeção dos dois tipos de ato do processo — o registro do que não cria versão e a versão do
/// edital — para o mesmo <see cref="AtoDoProcessoDto"/>.
/// </summary>
public static class AtoDoProcessoMapping
{
    public static AtoDoProcessoDto DoRegistro(
        AtoDoProcesso ato,
        IReadOnlyDictionary<Guid, DocumentoAPublicar> documentos,
        bool pendenciaAcimaDoLimiar)
    {
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(documentos);

        return new AtoDoProcessoDto(
            Id: ato.Id,
            CriaVersao: false,
            NumeroVersao: null,
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

    public static AtoDoProcessoDto DaVersao(
        ResumoDaVersao versao,
        SituacaoDoRegistroDoAto situacao,
        DocumentoAPublicar? documentoDoEdital,
        bool pendenciaAcimaDoLimiar)
    {
        ArgumentNullException.ThrowIfNull(versao);

        return new AtoDoProcessoDto(
            Id: versao.AtoCriadorId,
            CriaVersao: true,
            NumeroVersao: versao.NumeroVersao,
            TipoCodigo: null,
            TipoNome: null,
            Numero: null,
            DataDoAto: null,
            PublicadoEm: versao.VigenteAPartirDe,
            Assinante: null,
            Situacao: situacao,
            // O instante da confirmação não é guardado na divulgação do edital.
            RegistradoEm: null,
            PendenciaAcimaDoLimiar: pendenciaAcimaDoLimiar,
            AtoCorrigidoId: versao.AtoCriadorRetificaId,
            MotivoDaCorrecao: null,
            ProdutoDoEditalId: null,
            PoliticaDeDivulgacaoConfirmada: false,
            DocumentoPrincipal: documentoDoEdital is null ? null : Documento(documentoDoEdital, nomeDeExibicao: null),
            Anexos: []);
    }

    private static DocumentoDoAtoDto Documento(DocumentoAPublicar documento, string? nomeDeExibicao) =>
        new(documento.Id, documento.NomeArquivo, nomeDeExibicao, documento.TipoDeMidia, documento.HashSha256);
}
