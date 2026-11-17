namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

/// <summary>
/// Tira do documento os campos que o esquema marca como só de leitura (<c>id</c>, <c>versao</c>, <c>publicadoEm</c> e os
/// descritivos que a exportação traz ao lado de um código). Quem escreve o documento não os controla: o sistema os define.
/// </summary>
internal static class RetiradaDosCamposSomenteLeitura
{
    public static void Retirar(IReadOnlyList<NoDoDocumento> nos)
    {
        foreach (NoDoDocumento no in nos.Where(static no => no.Valor is JsonObject))
        {
            JsonObject objeto = (JsonObject)no.Valor;
            foreach (string nome in NomesSomenteLeitura(objeto, no.Forma))
            {
                objeto.Remove(nome);
            }
        }
    }

    private static List<string> NomesSomenteLeitura(JsonObject objeto, FormaDoNo forma) =>
        [.. objeto.Select(static propriedade => propriedade.Key).Where(nome => forma.Propriedades.TryGetValue(nome, out FormaDoNo? filho) && filho.SomenteLeitura)];
}
