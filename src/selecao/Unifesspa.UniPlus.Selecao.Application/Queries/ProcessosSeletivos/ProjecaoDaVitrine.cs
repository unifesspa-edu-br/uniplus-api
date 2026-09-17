namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Interfaces;

using DTOs;

/// <summary>
/// Projeta o item de lista da vitrine a partir do envelope congelado da versão publicamente visível.
/// </summary>
/// <remarks>
/// Campos declarados um a um, como no contrato de detalhe e pelo mesmo motivo: um item de lista
/// montado por recorte devolveria o bloco novo por omissão. E todo acesso de topo passa pela mesma
/// conferência de <see cref="ClassificacaoDosBlocosDoCertame"/> que o detalhe usa
/// (<see cref="ProjecaoDoCertamePublicado.BlocoPublico"/>): sem ela, classificar um bloco como
/// INTERNO não impediria a vitrine de publicá-lo, e a fronteira teria uma segunda porta sem
/// tranca.
/// <para>
/// <b>Um item malformado é omitido, não derruba a página.</b> É a diferença entre a vitrine e o
/// detalhe: lá, recusar é a resposta certa, porque o cidadão pediu aquele certame e meia projeção
/// mentiria sobre ele; aqui, um envelope defeituoso levaria consigo a lista inteira, e o resto dos
/// certames não tem culpa. O defeito aparece no detalhe, que recusa.
/// </para>
/// </remarks>
internal static class ProjecaoDaVitrine
{
    public static CertameNaVitrineDto? Projetar(CandidatoDaVitrine candidato, DateTimeOffset instante, JsonObject envelope)
    {
        if (!ProjecaoDoCertamePublicado.TentarObjeto(
                envelope, ProjecaoDoCertamePublicado.BlocoPublico("tipoProcesso"), out JsonObject? tipoNode)
            || !ProjecaoDoCertamePublicado.TentarTipoNomeado(tipoNode, out TipoCatalogadoCertameDto? tipoProcesso))
        {
            return null;
        }

        // O prazo vem do envelope ELEITO, não da coluna denormalizada da raiz: a coluna descreve a
        // publicação mais nova, e a versão publicamente visível pode ser anterior a ela enquanto o
        // ato da retificação não se registra. Servir a janela da retificação ali daria publicidade
        // a uma versão que ainda não tem nenhuma, e faria vitrine e detalhe discordarem.
        if (!ProjecaoDoCertamePublicado.TentarObjeto(
                envelope, ProjecaoDoCertamePublicado.BlocoPublico("periodo"), out JsonObject? periodo)
            || !ProjecaoDoCertamePublicado.TentarTextoOpcional(periodo, "numero", out string? numero)
            || !ProjecaoDoCertamePublicado.TentarInstante(periodo, "fim", out DateTimeOffset inscricoesAte))
        {
            return null;
        }

        if (!ProjecaoDoCertamePublicado.TentarTextos(
                envelope, ProjecaoDoCertamePublicado.BlocoPublico("modalidadesOfertadas"), out List<string>? modalidades))
        {
            return null;
        }

        if (!TotalDeVagas(envelope, out int totalDeVagas))
        {
            return null;
        }

        return new CertameNaVitrineDto(
            candidato.ProcessoSeletivoId,
            numero,
            candidato.Nome,
            tipoProcesso,
            modalidades,
            inscricoesAte,
            inscricoesAte >= instante,
            totalDeVagas);
    }

    /// <summary>
    /// Soma das vagas publicadas em todas as ofertas. Somar o total por oferta, e não o quadro por
    /// modalidade, é o que evita contar a mesma vaga duas vezes quando uma modalidade compõe outra.
    /// </summary>
    private static bool TotalDeVagas(JsonObject envelope, out int total)
    {
        total = 0;
        if (!ProjecaoDoCertamePublicado.TentarArray(
                envelope, ProjecaoDoCertamePublicado.BlocoPublico("vagas"), out JsonArray? vagas))
        {
            return false;
        }

        foreach (JsonNode? item in vagas)
        {
            if (item is not JsonObject configuracao
                || !ProjecaoDoCertamePublicado.TentarInteiro(configuracao, "totalPublicado", out int publicado))
            {
                return false;
            }

            total += publicado;
        }

        return true;
    }
}
