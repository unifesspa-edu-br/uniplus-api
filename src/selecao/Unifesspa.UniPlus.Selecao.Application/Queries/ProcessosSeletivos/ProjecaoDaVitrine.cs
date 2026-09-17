namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Interfaces;

using DTOs;

/// <summary>
/// Projeta o item de lista da vitrine a partir do envelope congelado da versão publicamente visível.
/// </summary>
/// <remarks>
/// Campos declarados um a um, como no contrato de detalhe e pelo mesmo motivo: um item de lista
/// montado por recorte devolveria o bloco novo por omissão.
/// <para>
/// <b>Um item malformado é omitido, não derruba a página.</b> É a diferença entre a vitrine e o
/// detalhe: lá, recusar é a resposta certa, porque o cidadão pediu aquele certame e meia projeção
/// mentiria sobre ele; aqui, um envelope defeituoso levaria consigo a lista inteira, e o resto dos
/// certames não tem culpa. O defeito aparece no detalhe, que recusa.
/// </para>
/// </remarks>
internal static class ProjecaoDaVitrine
{
    /// <summary>
    /// Lê o documento congelado, ou <see langword="null"/> quando ele não é um objeto legível. Uma
    /// linha adulterada direto no banco não pode derrubar a vitrine inteira com erro interno.
    /// </summary>
    public static JsonObject? TentarLer(string documentoCongelado)
    {
        try
        {
            return JsonNode.Parse(documentoCongelado) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool VersaoLegivel(IRegistroCodecsEnvelope registroCodecs, string schemaVersion) =>
        registroCodecs.Capacidades.Any(capacidade =>
            string.Equals(capacidade.SchemaVersion, schemaVersion, StringComparison.Ordinal) && capacidade.TemDecoder);

    public static CertameNaVitrineDto? Projetar(CandidatoDaVitrine candidato, DateTimeOffset instante, JsonObject envelope)
    {
        if (envelope.TryGetPropertyValue("tipoProcesso", out JsonNode? tipoNode) is false
            || tipoNode is not JsonObject tipo
            || !Texto(tipo, "codigo", out string tipoCodigo)
            || !Texto(tipo, "nome", out string tipoNome))
        {
            return null;
        }

        if (!envelope.TryGetPropertyValue("periodo", out JsonNode? periodoNode) || periodoNode is not JsonObject periodo)
        {
            return null;
        }

        // O número é opcional na publicação: chave ausente ou nula é estado válido.
        periodo.TryGetPropertyValue("numero", out JsonNode? numeroNode);
        string? numero = numeroNode is JsonValue jvNumero && jvNumero.TryGetValue(out string? lido) ? lido : null;

        if (!envelope.TryGetPropertyValue("modalidadesOfertadas", out JsonNode? modalidadesNode)
            || modalidadesNode is not JsonArray modalidadesArray)
        {
            return null;
        }

        List<string> modalidades = [];
        foreach (JsonNode? item in modalidadesArray)
        {
            if (item is not JsonValue valor || !valor.TryGetValue(out string? codigo))
            {
                return null;
            }

            modalidades.Add(codigo);
        }

        if (!TotalDeVagas(envelope, out int totalDeVagas))
        {
            return null;
        }

        return new CertameNaVitrineDto(
            candidato.ProcessoSeletivoId,
            numero,
            candidato.Nome,
            new TipoCatalogadoCertameDto(tipoCodigo, tipoNome),
            modalidades,
            candidato.InscricoesAte,
            candidato.InscricoesAte >= instante,
            totalDeVagas);
    }

    /// <summary>
    /// Soma das vagas publicadas em todas as ofertas. Somar o total por oferta, e não o quadro por
    /// modalidade, é o que evita contar a mesma vaga duas vezes quando uma modalidade compõe outra.
    /// </summary>
    private static bool TotalDeVagas(JsonObject envelope, out int total)
    {
        total = 0;
        if (!envelope.TryGetPropertyValue("vagas", out JsonNode? node) || node is not JsonArray vagas)
        {
            return false;
        }

        foreach (JsonNode? item in vagas)
        {
            if (item is not JsonObject configuracao
                || !configuracao.TryGetPropertyValue("totalPublicado", out JsonNode? totalNode)
                || totalNode is not JsonValue jv
                || !jv.TryGetValue(out int publicado))
            {
                return false;
            }

            total += publicado;
        }

        return true;
    }

    private static bool Texto(JsonObject objeto, string chave, out string valor)
    {
        valor = "";
        return objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor!);
    }
}
