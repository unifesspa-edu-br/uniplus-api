namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// Confere a recusa que saiu pelo registro de erros (ADR-0023): status HTTP e do corpo, mídia, code,
/// type na página do catálogo, title, detail, traceId sem o cabeçalho <c>traceparent</c> na resposta, e
/// instance. Só afirma o que a ADR-0023 e a implementação do host já concordam; o resto fica de fora,
/// cada item pelo seu motivo:
/// <list type="bullet">
/// <item><description><c>detail</c>: a ADR manda omiti-lo quando não acrescenta nada ao <c>title</c>, mas o
/// caminho do <c>DomainError</c> o emite sempre, com a mensagem do erro. O auxiliar aceita nulo (propriedade
/// ausente) para quando a implementação passar a omiti-lo.</description></item>
/// <item><description><c>traceId</c>: é o trace-id da atividade da requisição, que só é conhecido de fora
/// quando a requisição envia <c>traceparent</c>; sem atividade, o host gera um valor só para correlação com o
/// log. O auxiliar confere a forma (32 caracteres hexadecimais); a igualdade com o <c>traceparent</c> enviado
/// é conferida por um teste próprio no host.</description></item>
/// <item><description><c>errors</c>: sai sempre que a violação é atribuível a campo, em qualquer status
/// 4xx, e não sai quando ela não tem campo. O auxiliar confere a recusa raiz; quem provoca o erro de campo
/// confere a lista.</description></item>
/// <item><description><c>retryable</c>: sai só no conflito que o registro declara retentável. O auxiliar não
/// o confere; o teste desse conflito confere o sinal.</description></item>
/// <item><description><c>legal_reference</c>: a ADR o prevê para erro que decorre de regra normativa
/// rastreável, e o caminho do registro de erros não o emite. Não há o que conferir.</description></item>
/// <item><description><c>instance</c>: é opaca na ADR; o auxiliar confere só que está presente.</description></item>
/// </list>
/// </summary>
public static class ProblemaRegistrado
{
    private const string TraceparentCabecalho = "traceparent";

    public static async Task ConferirAsync(
        HttpResponseMessage resposta, HttpStatusCode status, string code, string title, string? detail)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        resposta.StatusCode.Should().Be(status);
        (resposta.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = corpo.RootElement;
        ConferirIdentificacao(raiz, status, code, title);
        ConferirDetail(raiz, detail);
        ConferirRastreio(resposta, raiz);
    }

    private static void ConferirIdentificacao(JsonElement raiz, HttpStatusCode status, string code, string title)
    {
        raiz.GetProperty("status").GetInt32().Should().Be((int)status);
        raiz.GetProperty("code").GetString().Should().Be(code);
        raiz.GetProperty("type").GetString().Should().Be(CatalogoDeErrosDoContrato.Base + code);
        raiz.GetProperty("title").GetString().Should().Be(title);
    }

    private static void ConferirDetail(JsonElement raiz, string? detail)
    {
        if (detail is null)
        {
            raiz.TryGetProperty("detail", out _).Should().BeFalse();
            return;
        }

        raiz.GetProperty("detail").GetString().Should().Be(detail);
    }

    private static void ConferirRastreio(HttpResponseMessage resposta, JsonElement raiz)
    {
        raiz.GetProperty("instance").GetString().Should().NotBeNullOrWhiteSpace();
        raiz.GetProperty("traceId").GetString().Should().MatchRegex("^[0-9a-f]{32}$");
        resposta.Headers.Contains(TraceparentCabecalho).Should().BeFalse("o traceId vai no corpo, não em cabeçalho");
    }
}
