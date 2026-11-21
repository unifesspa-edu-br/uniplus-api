namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

/// <summary>
/// O corpo que o cliente envia ao gravar o cronograma com a marca "no mesmo momento da anterior" e a leitura da recusa 422, que
/// serve também à recusa da publicação: a gravação e a publicação devolvem a lista <c>errors[]</c> no mesmo formato.
/// </summary>
internal static class CorpoDoCronogramaComMarca
{
    /// <summary>Um item de <c>identificadores</c>: a lista e o identificador do item que a recusa aponta.</summary>
    internal sealed record ItemIdentificado(string Lista, string Identificador);

    /// <summary>Um erro de <c>errors[]</c>; <see cref="Identificadores"/> é nulo quando a resposta não traz a propriedade.</summary>
    internal sealed record ErroDaRecusa(string Campo, string Codigo, string Mensagem, IReadOnlyList<ItemIdentificado>? Identificadores);

    /// <summary>
    /// Uma fase do corpo. A marca ausente (<c>null</c>) não escreve a propriedade, como o cliente que não a conhece. O período é
    /// de dez dias do mês informado, para fases em meses diferentes não se sobreporem.
    /// </summary>
    internal static JsonObject Fase(Guid faseCanonicaId, int ordem, int mes, bool? marca = null)
    {
        JsonObject fase = FaseSemAMarca(faseCanonicaId, ordem, mes);
        if (marca is bool declarada)
        {
            fase["noMesmoMomentoDaAnterior"] = declarada;
        }

        return fase;
    }

    internal static string Corpo(params JsonObject[] fases) => new JsonArray(fases).ToJsonString();

    /// <summary>Confere o 422 e devolve os erros na ordem em que a resposta os traz.</summary>
    internal static async Task<IReadOnlyList<ErroDaRecusa>> ErrosDaRecusaAsync(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);

        return [.. problema.RootElement.GetProperty("errors").EnumerateArray().Select(Erro)];
    }

    private static JsonObject FaseSemAMarca(Guid faseCanonicaId, int ordem, int mes) => new()
    {
        ["ordem"] = ordem,
        ["faseCanonicaId"] = faseCanonicaId,
        ["inicio"] = Instante(mes, 1),
        ["fim"] = Instante(mes, 10),
        ["produtos"] = new JsonArray(),
        ["emiteParecerIndividual"] = false,
        ["bancasRequeridas"] = new JsonArray(),
        ["regraRecurso"] = null,
    };

    private static string Instante(int mes, int dia) =>
        string.Create(CultureInfo.InvariantCulture, $"2027-{mes:D2}-{dia:D2}T00:00:00Z");

    private static ErroDaRecusa Erro(JsonElement erro) => new(
        erro.GetProperty("field").GetString()!,
        erro.GetProperty("code").GetString()!,
        erro.GetProperty("message").GetString()!,
        erro.TryGetProperty("identificadores", out JsonElement itens) ? [.. itens.EnumerateArray().Select(Item)] : null);

    private static ItemIdentificado Item(JsonElement item) =>
        new(item.GetProperty("lista").GetString()!, item.GetProperty("identificador").GetString()!);
}
