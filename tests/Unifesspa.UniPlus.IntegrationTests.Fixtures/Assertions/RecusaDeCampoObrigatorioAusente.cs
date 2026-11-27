namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// A recusa, no envelope da ADR-0023, do corpo que omite um campo obrigatório ou o traz com <c>null</c>:
/// 400, um item de <c>errors[]</c> com o código de campo obrigatório ausente e o ponteiro do campo em
/// camelCase, e nada do formato padrão do framework.
/// </summary>
public static class RecusaDeCampoObrigatorioAusente
{
    public const string Code = "uniplus.requisicao.campo_obrigatorio_ausente";

    public static async Task ConferirAsync(HttpResponseMessage resposta, string campo)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        await ProblemaRegistrado.ConferirAsync(
            resposta,
            HttpStatusCode.BadRequest,
            Code,
            "A requisição não declara campo obrigatório do contrato",
            $"A requisição não traz valor para o campo obrigatório '{campo}'.");

        await ConferirItemDeErroAsync(resposta, campo);
    }

    private static async Task ConferirItemDeErroAsync(HttpResponseMessage resposta, string campo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        using JsonDocument problema = JsonDocument.Parse(corpo);
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Should().ContainSingle();
        erros[0].GetProperty("field").GetString().Should().Be(campo);
        erros[0].GetProperty("code").GetString().Should().Be(Code);
        corpo.Should().NotContain("field is required").And.NotContain("One or more validation errors");
    }
}
