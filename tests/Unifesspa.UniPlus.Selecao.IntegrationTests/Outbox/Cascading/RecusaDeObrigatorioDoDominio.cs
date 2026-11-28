namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// A recusa do domínio para o campo obrigatório ausente ou nulo: 422, código genérico de validação
/// obrigatória e um item de <c>errors[]</c> por campo, na ordem em que o domínio os acumula.
/// </summary>
internal static class RecusaDeObrigatorioDoDominio
{
    public const string Codigo = "uniplus.validacao.obrigatorio";

    public static async Task ConferirAsync(HttpResponseMessage resposta, params string[] campos)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be(Codigo);
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => e.GetProperty("field").GetString()).Should().Equal(campos);
        erros.Should().OnlyContain(static e => e.GetProperty("code").GetString() == Codigo);
    }
}
