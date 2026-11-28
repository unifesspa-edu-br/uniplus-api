namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

internal static class SerializacaoDaImportacaoDoResultado
{
    internal static JsonObject? Serializar(DeclaracaoDaImportacaoDoResultado? declaracao) => declaracao is null
        ? null
        : new()
        {
            ["relatorios"] = new JsonArray([.. declaracao.Relatorios.Select(SerializarRelatorio)]),
            ["primeiraFaseDepoisDaImportacaoId"] = declaracao.PrimeiraFaseDepoisDaImportacaoId,
        };

    private static JsonNode SerializarRelatorio(RelatorioDeclarado relatorio) => new JsonObject
    {
        ["leiauteCodigo"] = relatorio.LeiauteCodigo,
        ["leiauteVersao"] = relatorio.LeiauteVersao,
    };
}
