namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>Uma das três recusas de cursor, com o ProblemDetails inteiro que a listagem devolve.</summary>
public sealed record RecusaDeCursor(HttpStatusCode Status, string Code, string Title, string Detail)
{
    private const string BaseDoTipo = "/erros/";

    /// <summary>O cursor é da coleção certa, mas de outra consulta: ordenação, filtro, busca ou recorte.</summary>
    public static RecusaDeCursor DeOutraConsulta { get; } = new(
        HttpStatusCode.BadRequest,
        "uniplus.paginacao.cursor_invalido",
        "Cursor inválido",
        "O cursor informado não continua esta consulta. Refaça a listagem sem cursor.");

    /// <summary>Não decifra, é de outra coleção ou escopo, de outra direção ou de outra conta, ou não traz a chave.</summary>
    public static RecusaDeCursor Invalida { get; } = new(
        HttpStatusCode.BadRequest,
        "uniplus.cursor.invalido",
        "Cursor de paginação inválido",
        "O cursor informado é inválido.");

    public static RecusaDeCursor Expirada { get; } = new(
        HttpStatusCode.Gone,
        "uniplus.cursor.expirado",
        "Cursor de paginação expirado",
        "O cursor informado expirou.");

    public async Task ConferirAsync(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        string corpo = await RespostaDaListagem.ConferirStatusAsync(resposta, Status).ConfigureAwait(false);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        JsonElement raiz = problema.RootElement;
        raiz.GetProperty("status").GetInt32().Should().Be((int)Status);
        raiz.GetProperty("code").GetString().Should().Be(Code);
        raiz.GetProperty("type").GetString().Should().EndWith(BaseDoTipo + Code);
        raiz.GetProperty("title").GetString().Should().Be(Title);
        raiz.GetProperty("detail").GetString().Should().Be(Detail);
    }
}
