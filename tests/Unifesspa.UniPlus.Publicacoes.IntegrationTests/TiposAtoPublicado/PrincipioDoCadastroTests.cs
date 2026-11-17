namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static TipoAtoPublicadoApoio;

/// <summary>
/// O princípio da ADR-0103, exercitado pela API: acrescentar um tipo de ato é linha
/// de cadastro, e dois tipos com a mesma configuração comportam-se identicamente.
/// </summary>
/// <remarks>
/// <para>A prova estrutural — nenhum <c>if</c> por código de tipo em
/// <c>src/publicacoes/</c> — vive no fitness test
/// <c>PublicacoesSemRamificacaoPorTipoAtoTests</c>. Aqui prova-se o comportamento:
/// um código nunca visto pelo código-fonte percorre todo o ciclo de vida sem que
/// nada precise saber o que ele significa.</para>
/// <para>Se um ramo por código existisse, os dois tipos desta suíte divergiriam —
/// e nenhum teste de status os pegaria, porque ambos responderiam 200.</para>
/// </remarks>
[Collection(PublicacoesEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PrincipioDoCadastroTests
{
    private static readonly string[] AtributosDeConsequencia = ["congelaConfiguracao", "unicoPorObjeto", "efeitoIrreversivel"];

    private readonly PublicacoesEndpointFixture _fixture;

    public PrincipioDoCadastroTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    /// <remarks>Mesmas chaves, mesma forma: o que varia é a identidade e o rótulo.</remarks>
    [Fact(DisplayName = "Dois tipos com a mesma configuração têm a mesma forma")]
    public async Task DoisTiposComMesmaConfiguracao_TemAMesmaForma()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        ParDeTipos par = await CadastrarParAsync(client);

        JsonElement corpoAlfa = await LerAsync(client, $"{Base}/{par.IdAlfa}");
        JsonElement corpoBeta = await LerAsync(client, $"{Base}/{par.IdBeta}");

        Nomes(corpoAlfa).Should().BeEquivalentTo(Nomes(corpoBeta));
    }

    [Fact(DisplayName = "Dois tipos com a mesma configuração têm os mesmos atributos de consequência")]
    public async Task DoisTiposComMesmaConfiguracao_TemOsMesmosAtributos()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        ParDeTipos par = await CadastrarParAsync(client);

        JsonElement corpoAlfa = await LerAsync(client, $"{Base}/{par.IdAlfa}");
        JsonElement corpoBeta = await LerAsync(client, $"{Base}/{par.IdBeta}");

        foreach (string campo in AtributosDeConsequencia)
        {
            corpoAlfa.GetProperty(campo).GetBoolean()
                .Should().Be(corpoBeta.GetProperty(campo).GetBoolean(), $"{campo} foi informado igual nos dois");
        }
    }

    [Fact(DisplayName = "Dois tipos com a mesma configuração são resolvidos pela data do mesmo jeito")]
    public async Task DoisTiposComMesmaConfiguracao_SaoResolvidosPelaData()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        ParDeTipos par = await CadastrarParAsync(client);

        (await LerAsync(client, $"{Base}/{par.CodigoAlfa}/vigente")).GetProperty("id").GetGuid().Should().Be(par.IdAlfa);
        (await LerAsync(client, $"{Base}/{par.CodigoBeta}/vigente")).GetProperty("id").GetGuid().Should().Be(par.IdBeta);
    }

    [Fact(DisplayName = "Dois tipos com a mesma configuração são removidos do mesmo jeito")]
    public async Task DoisTiposComMesmaConfiguracao_SaoRemovidosDoMesmoJeito()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        ParDeTipos par = await CadastrarParAsync(client);

        (await RemoverAsync(client, par.IdAlfa)).Should().Be(await RemoverAsync(client, par.IdBeta));
    }

    /// <remarks>
    /// Nenhum arquivo de <c>src/publicacoes/</c> conhece o código usado: se acrescentar um tipo exigisse
    /// alteração no domínio, estes testes falhariam.
    /// </remarks>
    [Fact(DisplayName = "Um código que o código-fonte nunca viu é cadastrado, lido e resolvido pela data")]
    public async Task CodigoDesconhecido_ECadastradoELido()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid id = await CadastrarAsync(client, new CorpoDoTipoDeAto(codigo) { CongelaConfiguracao = false });

        (await LerAsync(client, $"{Base}/{id}")).GetProperty("codigo").GetString().Should().Be(codigo);
        (await LerAsync(client, $"{Base}/{codigo}/vigente")).GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact(DisplayName = "Um código que o código-fonte nunca viu muda os atributos de consequência por PUT")]
    public async Task CodigoDesconhecido_MudaOsAtributosPorPut()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, new CorpoDoTipoDeAto(codigo) { CongelaConfiguracao = false });

        CorpoDoTipoDeAto corpo = ComTodasAsConsequencias(codigo) with { Id = id, Nome = "Renomeado sem que nada saiba o que ele é" };
        (await AtualizarAsync(client, id, corpo)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await LerAsync(client, $"{Base}/{id}")).GetProperty("congelaConfiguracao").GetBoolean().Should().BeTrue(
            "os atributos de consequência são dados: mudam por PUT, não por deploy");
    }

    [Fact(DisplayName = "Um código que o código-fonte nunca viu é removido")]
    public async Task CodigoDesconhecido_ERemovido()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, new CorpoDoTipoDeAto(CodigoUnico()) { CongelaConfiguracao = false });

        (await RemoverAsync(client, id)).Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<ParDeTipos> CadastrarParAsync(HttpClient client)
    {
        string alfa = CodigoUnico();
        string beta = CodigoUnico();

        return new ParDeTipos(
            alfa, await CadastrarAsync(client, ComTodasAsConsequencias(alfa)),
            beta, await CadastrarAsync(client, ComTodasAsConsequencias(beta)));
    }

    private static CorpoDoTipoDeAto ComTodasAsConsequencias(string codigo) =>
        new(codigo) { UnicoPorObjeto = true, EfeitoIrreversivel = true };

    private static async Task<JsonElement> LerAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri(url, UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task<HttpStatusCode> RemoverAsync(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"{Admin}/{id}", UriKind.Relative));
        Autenticar(request);

        return (await client.SendAsync(request)).StatusCode;
    }

    private static IReadOnlyList<string> Nomes(JsonElement corpo) =>
        [.. corpo.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];

    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "PRINCIPIO_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }

    private sealed record ParDeTipos(string CodigoAlfa, Guid IdAlfa, string CodigoBeta, Guid IdBeta);
}
