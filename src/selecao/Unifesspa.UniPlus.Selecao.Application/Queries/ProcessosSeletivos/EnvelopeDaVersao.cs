namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;

/// <summary>A leitura segura da configuração congelada de uma versão.</summary>
internal static class EnvelopeDaVersao
{
    /// <summary>O envelope da versão, ou <see langword="null"/> quando não há versão ou o conteúdo não é um objeto legível.</summary>
    public static JsonObject? Ler(VersaoConfiguracao? versao)
    {
        if (versao is null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(versao.ConfiguracaoCongelada) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
