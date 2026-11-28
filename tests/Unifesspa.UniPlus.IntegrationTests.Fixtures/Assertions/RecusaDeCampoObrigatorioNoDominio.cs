namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// A recusa, no envelope da ADR-0023, do corpo que omite um campo obrigatório ou o traz com <c>null</c> quando o
/// campo chega ao domínio: 422 e um item de <c>errors[]</c> com o código genérico de campo obrigatório e o ponteiro
/// do campo em camelCase, na ordem em que o domínio as acumula.
/// </summary>
public static class RecusaDeCampoObrigatorioNoDominio
{
    public const string Code = "uniplus.validacao.obrigatorio";

    /// <summary>A resposta recusa só o campo informado, como obrigatório.</summary>
    public static Task ConferirAsync(HttpResponseMessage resposta, string campo) =>
        ConferirCamposAsync(resposta, campo);

    /// <summary>A resposta recusa exatamente estes campos, como obrigatórios e nesta ordem.</summary>
    public static async Task ConferirCamposAsync(HttpResponseMessage resposta, params string[] campos)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => e.GetProperty("field").GetString()).Should().Equal(campos);
        erros.Select(static e => e.GetProperty("code").GetString()).Should().OnlyContain(codigo => codigo == Code);
    }

    /// <summary>A resposta recusa os campos, na ordem, e cada um com o código dado.</summary>
    public static async Task ConferirRecusasAsync(HttpResponseMessage resposta, params (string Campo, string Codigo)[] recusas)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString()!, e.GetProperty("code").GetString()!))
            .Should().Equal(recusas);
    }
}
