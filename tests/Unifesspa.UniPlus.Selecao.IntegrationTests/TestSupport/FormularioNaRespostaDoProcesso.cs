namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Text.Json;

/// <summary>
/// O formulário na resposta do GET do processo: o de inscrição, a coleta dele — os itens e os grupos — e cada item pelo fato.
/// </summary>
internal static class FormularioNaRespostaDoProcesso
{
    public static JsonElement DeInscricao(JsonDocument processo) =>
        processo.RootElement.GetProperty("formularios").EnumerateArray()
            .Single(static f => f.GetProperty("finalidade").GetProperty("codigo").GetString() == "INSCRICAO");

    public static IEnumerable<JsonElement> Itens(JsonElement formulario) =>
        formulario.GetProperty("coleta").GetProperty("itens").EnumerateArray();

    public static IEnumerable<JsonElement> Grupos(JsonElement formulario) =>
        formulario.GetProperty("coleta").GetProperty("grupos").EnumerateArray();

    public static JsonElement Item(JsonElement formulario, string fatoCodigo) =>
        Itens(formulario).Single(f => f.GetProperty("fatoCodigo").GetString() == fatoCodigo);
}
