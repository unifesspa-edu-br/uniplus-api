namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento.RequisicoesDeTermoConsentimento;

/// <summary>Continuar o cursor emitido para uma busca de termos de consentimento sob outra busca.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TrocaDeBuscaNoCursorDeTermosTests : IAsyncLifetime
{
    private const string Colecao = "/api/configuracao/termos-consentimento";

    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosCursoresDeConfiguracao _apoio;
    private readonly List<Guid> _criados = [];

    public TrocaDeBuscaNoCursorDeTermosTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosCursoresDeConfiguracao(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverTermosAsync(_criados);

    [Fact(DisplayName = "O cursor emitido para uma busca de termos é recusado quando continuado com outra busca")]
    public async Task Listar_CursorDeUmaBuscaContinuadoComOutra_Retorna400() =>
        (await ContinuarComAsync(static _ => "outrabusca")).Should().Be(HttpStatusCode.BadRequest);

    [Fact(DisplayName = "O cursor emitido para uma busca de termos continua com a mesma busca, sem contar os espaços das pontas")]
    public async Task Listar_CursorDeUmaBuscaContinuadoComEspacosNasPontas_Retorna200() =>
        (await ContinuarComAsync(static marca => $"%20%20{marca}%20")).Should().Be(HttpStatusCode.OK);

    private async Task<HttpStatusCode> ContinuarComAsync(Func<string, string> buscaDaContinuacao)
    {
        string marca = Guid.NewGuid().ToString("N")[..10];
        using HttpClient client = _fixture.Factory.CreateClient();
        _criados.Add(await CriarTermoComNome(client, $"Termo {marca} Um"));
        _criados.Add(await CriarTermoComNome(client, $"Termo {marca} Dois"));
        using HttpResponseMessage primeira = await client.GetAsync(new Uri($"{Colecao}?q={marca}&limit=1", UriKind.Relative));
        string? proxima = ApoioDaOrdenacaoAlfabetica.LinkRel(primeira, "next");
        proxima.Should().NotBeNull("limit=1 com 2 termos na busca deve emitir próxima página");

        using HttpResponseMessage seguinte = await client.GetAsync(
            new Uri(proxima!.Replace($"q={marca}", $"q={buscaDaContinuacao(marca)}", StringComparison.Ordinal), UriKind.RelativeOrAbsolute));

        return seguinte.StatusCode;
    }
}
