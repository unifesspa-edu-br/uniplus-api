namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using DTOs;

/// <summary>
/// Leitura, no envelope congelado, do atendimento especializado ofertado: as condições, os recursos e os tipos de
/// deficiência contemplados.
/// </summary>
internal static class LeituraDoAtendimentoDoCertame
{
    internal static bool TentarAtendimento(JsonObject envelope, [NotNullWhen(true)] out AtendimentoCertameDto? atendimento)
    {
        atendimento = null;
        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("atendimento"), out JsonObject? bloco)
            || !TentarParesNomeados(bloco, "condicoes", "condicaoCodigo", "condicaoNome", out List<CondicaoAtendimentoCertameDto>? condicoes)
            || !TentarParesNomeados(bloco, "tiposDeficiencia", "tipoDeficienciaCodigo", "tipoDeficienciaNome", out List<CondicaoAtendimentoCertameDto>? tipos)
            || !TentarRecursos(bloco, out List<string>? recursos))
        {
            return false;
        }

        atendimento = new AtendimentoCertameDto(condicoes, recursos, tipos);
        return true;
    }

    private static bool TentarRecursos(JsonObject bloco, [NotNullWhen(true)] out List<string>? recursos)
    {
        recursos = null;
        return PrimitivasDaProjecao.TentarArray(bloco, "recursos", out JsonArray? array)
            && PrimitivasDaProjecao.TentarItens<string>(array, TentarRecurso, out recursos);
    }

    private static bool TentarRecurso(JsonNode? item, [NotNullWhen(true)] out string? nome)
    {
        nome = null;
        return item is JsonObject recurso && PrimitivasDaProjecao.TentarTexto(recurso, "recursoNome", out nome);
    }

    private static bool TentarParesNomeados(
        JsonObject bloco,
        string chaveArray,
        string chaveCodigo,
        string chaveNome,
        [NotNullWhen(true)] out List<CondicaoAtendimentoCertameDto>? pares)
    {
        pares = null;
        return PrimitivasDaProjecao.TentarArray(bloco, chaveArray, out JsonArray? array)
            && PrimitivasDaProjecao.TentarItens<CondicaoAtendimentoCertameDto>(array, LerPar, out pares);

        bool LerPar(JsonNode? item, [NotNullWhen(true)] out CondicaoAtendimentoCertameDto? par) =>
            TentarPar(item, chaveCodigo, chaveNome, out par);
    }

    private static bool TentarPar(JsonNode? item, string chaveCodigo, string chaveNome, [NotNullWhen(true)] out CondicaoAtendimentoCertameDto? par)
    {
        par = null;
        if (item is not JsonObject entrada
            || !PrimitivasDaProjecao.TentarTexto(entrada, chaveCodigo, out string codigo)
            || !PrimitivasDaProjecao.TentarTexto(entrada, chaveNome, out string nome))
        {
            return false;
        }

        par = new CondicaoAtendimentoCertameDto(codigo, nome);
        return true;
    }
}
