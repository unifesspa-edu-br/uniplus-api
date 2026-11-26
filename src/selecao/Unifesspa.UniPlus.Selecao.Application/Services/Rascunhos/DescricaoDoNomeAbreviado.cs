namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

/// <summary>
/// Acrescenta à divulgação a regra que abrevia o nome do candidato, quando o nome abreviado é um dos campos públicos. É a regra
/// vigente no momento da exportação, a mesma que a publicação congela; sem o nome abreviado entre os campos, nada é acrescentado.
/// </summary>
public static class DescricaoDoNomeAbreviado
{
    private const string CampoDosCamposPublicos = "camposPublicos";

    public static void Descrever(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento["divulgacao"] is JsonObject divulgacao && DivulgaONomeAbreviado(divulgacao))
        {
            divulgacao.Insert(divulgacao.IndexOf(CampoDosCamposPublicos) + 1, "regraNomeAbreviado", RegrasDeNomeAbreviado.Vigente);
        }
    }

    private static bool DivulgaONomeAbreviado(JsonObject divulgacao) =>
        divulgacao[CampoDosCamposPublicos] is JsonArray campos
        && campos.Any(static campo => campo is JsonValue valor && valor.TryGetValue(out string? texto)
            && string.Equals(texto, ConfiguracaoDivulgacao.NomeAbreviado, StringComparison.Ordinal));
}
