namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

/// <summary>
/// Prova, contra o OpenAPI de runtime, as duas afirmações que a #804 faz sobre o contrato
/// HTTP — e que a ADR-0103 exige que sejam <b>provadas</b>, não afirmadas.
/// </summary>
/// <remarks>
/// <list type="number">
///   <item><b>Publicar e retificar recebem o MESMO bloco <c>ato</c>.</b> Se retificar fosse
///     um tipo de ato, o contrato o denunciaria: ou o bloco seria diferente, ou o tipo seria
///     inferido pelo servidor num dos dois. Ele não é — o operador declara o tipo nos dois
///     casos, e o catálogo de Publicações o confere. Uma convocação retificada continua
///     convocação.</item>
///   <item><b>O servidor infere o alvo da retificação</b> (ADR-0101): o corpo não tem, e não
///     pode ter, um campo com o id do ato emendado. O alvo é o ato criador da versão
///     corrente — estado do agregado, nunca input do cliente.</item>
/// </list>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ContratoPublicarRetificarTests
{
    private const string PathPublicar = "/api/selecao/processos-seletivos/{id}/publicacao";
    private const string PathRetificar = "/api/selecao/processos-seletivos/{id}/retificacoes";

    private readonly CascadingFixture _fixture;

    public ContratoPublicarRetificarTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Publicar e retificar declaram o MESMO bloco 'ato' — retificar não é um tipo de ato (ADR-0103)")]
    public async Task PublicarERetificar_CompartilhamOBlocoDoAto()
    {
        using JsonDocument spec = await ObterSpecAsync();

        string schemaPublicar = SchemaDoCorpo(spec, PathPublicar);
        string schemaRetificar = SchemaDoCorpo(spec, PathRetificar);

        JsonElement componentes = spec.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement corpoPublicar = componentes.GetProperty(schemaPublicar);
        JsonElement corpoRetificar = componentes.GetProperty(schemaRetificar);

        // O bloco `ato` é o mesmo tipo nos dois — não uma cópia parecida.
        string refPublicar = RefDoAto(corpoPublicar);
        string refRetificar = RefDoAto(corpoRetificar);
        refRetificar.Should().Be(
            refPublicar,
            "o bloco documental do ato é o MESMO nos dois endpoints: o que muda entre publicar e "
            + "retificar é a relação com o ato anterior, não o que o ato é");

        // E o tipo do ato é declarado pelo operador nos dois — nunca inferido.
        JsonElement blocoDoAto = componentes.GetProperty(refPublicar);
        PropriedadesDe(blocoDoAto).Should().Contain(
            "tipoAtoCodigo",
            "o tipo do ato vem do catálogo de Publicações, declarado por quem publica (ADR-0103)");

        // Equivalência PROVADA, não afirmada: os dois corpos são comparados schema a schema
        // depois de removido o `motivo`. Conferir só os nomes das propriedades deixaria passar
        // divergência de tipo, formato, nulabilidade ou obrigatoriedade — e um campo que mudou
        // de `string` para `uuid`, ou de opcional para obrigatório, é uma quebra de contrato
        // tão real quanto um campo a mais.
        (string Nome, string Json)[] publicarSemMotivo = [.. PropriedadesComSchema(corpoPublicar)];
        (string Nome, string Json)[] retificarSemMotivo =
            [.. PropriedadesComSchema(corpoRetificar).Where(p => p.Nome != "motivo")];

        retificarSemMotivo.Should().BeEquivalentTo(
            publicarSemMotivo,
            "retirado o motivo, o corpo da retificação é byte-a-byte o da publicação: retificar é "
            + "publicar um ato que emenda outro, e nada mais o distingue");

        // E o motivo é a única adição — obrigatório, porque a relação é o par (ato, motivo).
        Obrigatorias(corpoRetificar).Except(Obrigatorias(corpoPublicar)).Should().BeEquivalentTo(
            ["motivo"],
            "a retificação exige motivo, e é a única exigência a mais que ela faz");
    }

    [Fact(DisplayName = "O corpo da retificação não carrega o id do ato emendado — o servidor o infere (ADR-0101)")]
    public async Task Retificar_NaoRecebeOAlvoDoCliente()
    {
        using JsonDocument spec = await ObterSpecAsync();

        JsonElement corpoRetificar = spec.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty(SchemaDoCorpo(spec, PathRetificar));

        IEnumerable<string> propriedades = PropriedadesDe(corpoRetificar);

        // O alvo é o ato criador da versão corrente — o topo da cadeia, que só o servidor
        // conhece. Aceitá-lo do cliente abriria a porta para emendar um ato que não é o
        // vigente (ramificando a cadeia) e vazaria a linhagem interna no contrato.
        propriedades.Should().NotContain(
            nome => nome.Contains("retificad", StringComparison.OrdinalIgnoreCase)
                || nome.Contains("atoAnterior", StringComparison.OrdinalIgnoreCase)
                || nome.Contains("editalRetificado", StringComparison.OrdinalIgnoreCase),
            "o cliente endereça o PROCESSO; quem elege o ato emendado é o servidor");
    }

    private async Task<JsonDocument> ObterSpecAsync()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await client.GetAsync(new Uri("/openapi/selecao.json", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>Nome do schema do corpo (<c>requestBody</c>) do <c>POST</c> de um path.</summary>
    private static string SchemaDoCorpo(JsonDocument spec, string path)
    {
        JsonElement schema = spec.RootElement
            .GetProperty("paths").GetProperty(path)
            .GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json")
            .GetProperty("schema");

        return NomeDoRef(schema);
    }

    /// <summary>Nome do schema referenciado pela propriedade <c>ato</c> de um corpo.</summary>
    private static string RefDoAto(JsonElement corpo) =>
        NomeDoRef(corpo.GetProperty("properties").GetProperty("ato"));

    private static string NomeDoRef(JsonElement elemento)
    {
        string referencia = elemento.GetProperty("$ref").GetString()!;
        return referencia[(referencia.LastIndexOf('/') + 1)..];
    }

    private static IEnumerable<string> PropriedadesDe(JsonElement schema) =>
        schema.GetProperty("properties").EnumerateObject().Select(p => p.Name);

    /// <summary>
    /// Cada propriedade com o seu schema COMPLETO, em JSON cru — tipo, formato, nulabilidade e
    /// o que mais o OpenAPI declarar. É o que permite comparar os dois corpos de verdade, e
    /// não só pelos nomes.
    /// </summary>
    private static IEnumerable<(string Nome, string Json)> PropriedadesComSchema(JsonElement schema) =>
        schema.GetProperty("properties").EnumerateObject()
            .Select(p => (p.Name, p.Value.GetRawText()));

    private static IEnumerable<string> Obrigatorias(JsonElement schema) =>
        schema.TryGetProperty("required", out JsonElement required)
            ? required.EnumerateArray().Select(e => e.GetString()!)
            : [];
}
