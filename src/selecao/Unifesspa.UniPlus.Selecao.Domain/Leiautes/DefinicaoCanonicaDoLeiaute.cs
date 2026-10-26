namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Calcula o hash de um leiaute sobre a sua definição: o que muda a leitura do arquivo entra; o
/// que só descreve ou ativa o leiaute (descrição, <c>Ativo</c>) fica de fora, porque não é
/// identidade.
/// </summary>
internal static class DefinicaoCanonicaDoLeiaute
{
    public static string Hash(
        string codigo,
        string versao,
        TipoDeLeiauteDoInep tipo,
        string separador,
        string fimDeLinha,
        int quantidadeDeCampos,
        IEnumerable<ColunaDoRetorno> colunas)
    {
        JsonArray colunasJson = [];
        foreach (ColunaDoRetorno coluna in colunas)
        {
            JsonObject parametros = [];
            foreach (KeyValuePair<string, string> parametro in coluna.Leitor.Parametros)
            {
                parametros[parametro.Key] = parametro.Value;
            }

            colunasJson.Add(new JsonObject
            {
                ["area"] = coluna.Area,
                ["campo"] = coluna.Campo.ToString(),
                ["leitor"] = coluna.Leitor.Codigo,
                ["parametros"] = parametros,
                ["posicao"] = coluna.Posicao,
            });
        }

        JsonObject definicao = new()
        {
            ["codificacao"] = "utf-8",
            ["codigo"] = codigo,
            ["colunas"] = colunasJson,
            ["fimDeLinha"] = fimDeLinha,
            ["quantidadeDeCampos"] = quantidadeDeCampos,
            ["separador"] = separador,
            ["tipo"] = tipo.ToString(),
            ["versao"] = versao,
        };

        return HashCanonicalComputer.ComputeSha256Hex(HashCanonicalComputer.ComputeSnapshotBytes(definicao));
    }
}
