namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// O exemplo que a documentação do parâmetro <c>sort</c> mostra é executado
/// contra a própria rota que o documenta.
/// </summary>
/// <remarks>
/// A descrição do parâmetro é escrita à mão, e cada rota aceita campos diferentes:
/// nada impede que o exemplo de uma acabe citando os campos de outra. Quando isso
/// acontece, quem copia da documentação recebe 422 — e nenhum teste de caminho
/// feliz percebe, porque o exemplo não é código.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed partial class ExemploDocumentadoTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ExemploDocumentadoTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "O exemplo de sort que a rota documenta funciona nela mesma")]
    [InlineData("/api/configuracao/cursos")]
    [InlineData("/api/configuracao/ofertas-curso")]
    public async Task ExemploDeSort_FuncionaNaPropriaRota(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        string exemplo = await ExemploDeSortDaRotaAsync(client, rota);

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{rota}?sort={Uri.EscapeDataString(exemplo)}", UriKind.Relative));

        resposta.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "quem copia '{0}' da documentação de {1} precisa receber a listagem, não uma recusa",
            exemplo,
            rota);
    }

    /// <summary>Lê da especificação publicada o exemplo que a rota anuncia.</summary>
    private static async Task<string> ExemploDeSortDaRotaAsync(HttpClient client, string rota)
    {
        string spec = await client.GetStringAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));

        int inicioDaRota = spec.IndexOf($"\"{rota}\"", StringComparison.Ordinal);
        inicioDaRota.Should().BeGreaterThan(-1, "a rota {0} precisa estar na especificação", rota);

        // A descrição do parâmetro sort da rota é a primeira ocorrência depois dela.
        Match exemplo = ExemploDeSort().Match(spec, inicioDaRota);
        exemplo.Success.Should().BeTrue("a documentação de {0} precisa mostrar um exemplo de sort", rota);

        return exemplo.Groups["expressao"].Value;
    }

    [GeneratedRegex(@"Exemplo: sort=(?<expressao>[^.\\""]+)")]
    private static partial Regex ExemploDeSort();
}
