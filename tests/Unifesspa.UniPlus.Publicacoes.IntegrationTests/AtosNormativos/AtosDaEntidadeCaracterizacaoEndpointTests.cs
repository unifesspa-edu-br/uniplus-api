namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static AtosDaEntidadeApoio;

/// <summary>
/// O resultado de <c>GET /entidades/{entidadeTipo}/{entidadeId}/atos</c> nos pontos em que a consulta é
/// montada com a entidade como par: o empate no instante da divulgação atravessando o limite da página, a
/// volta da segunda página para a primeira e a leitura que não aplica a regra de gravação ao par.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class AtosDaEntidadeCaracterizacaoEndpointTests
{
    private static readonly DateTimeOffset Divulgacao = new(2026, 3, 13, 15, 0, 0, TimeSpan.Zero);

    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtosDaEntidadeApoio _apoio;

    public AtosDaEntidadeCaracterizacaoEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtosDaEntidadeApoio(_fixture);
    }

    /// <remarks>
    /// Os dois primeiros atos têm o mesmo instante de divulgação, e o limite de uma página separa um do outro: o
    /// identificador desempata, e nenhum ato se repete nem se perde na passagem.
    /// </remarks>
    [Fact(DisplayName = "O empate no instante da divulgação atravessa o limite da página sem repetir nem perder ato")]
    public async Task Listar_EmpateNoLimiteDaPagina_PercorreEmOrdemDoIdentificador()
    {
        Guid processo = Guid.CreateVersion7();
        Guid[] ids = await RegistrarTresAtosComEmpateNosDoisPrimeirosAsync(processo);
        using HttpClient client = _fixture.Factory.CreateClient();

        List<Guid> percorridos = await PercorrerDePaginaEmPaginaAsync(client, processo);

        percorridos.Should().Equal(ids);
    }

    [Fact(DisplayName = "O link anterior da segunda página volta à primeira, com o mesmo ato")]
    public async Task Listar_AnteriorDaSegundaPagina_VoltaAPrimeira()
    {
        Guid processo = Guid.CreateVersion7();
        Guid[] ids = await RegistrarTresAtosComEmpateNosDoisPrimeirosAsync(processo);
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeira = await ListarPaginaAsync(client, $"/api/publicacoes/entidades/{EntidadesDeRegistroPeloDominio.ProcessoSeletivo}/{processo}/atos?limit=1");
        HttpResponseMessage segunda = await ListarPaginaAsync(client, AtoNormativoApoio.LinkDe(primeira, "next")!);

        HttpResponseMessage volta = await ListarPaginaAsync(client, AtoNormativoApoio.LinkDe(segunda, "prev")!);

        (await IdsDaPaginaAsync(segunda)).Should().Equal(ids[1]);
        (await IdsDaPaginaAsync(volta)).Should().Equal(ids[0]);
    }

    [Fact(DisplayName = "Rótulo com mais de 60 caracteres na forma canônica devolve a coleção vazia, e não 400")]
    public async Task Listar_RotuloLongoNaFormaCanonica_DevolveVazio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        JsonElement[] itens = await ListarDaEntidadeAsync(client, new string('A', 61), Guid.CreateVersion7());

        itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Identificador vazio de entidade devolve a coleção vazia, e não 400")]
    public async Task Listar_IdentificadorVazio_DevolveVazio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        JsonElement[] itens = await ListarDaEntidadeAsync(client, ObjetoA, Guid.Empty);

        itens.Should().BeEmpty();
    }

    private async Task<Guid[]> RegistrarTresAtosComEmpateNosDoisPrimeirosAsync(Guid processo)
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid[] ids = [.. new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() }.Order()];
        await _apoio.RegistrarPeloDominioAsync(tipo, "1", processo, ids[1], Divulgacao);
        await _apoio.RegistrarPeloDominioAsync(tipo, "2", processo, ids[0], Divulgacao);
        await _apoio.RegistrarPeloDominioAsync(tipo, "3", processo, ids[2], Divulgacao.AddMinutes(1));

        return ids;
    }

    private static async Task<List<Guid>> PercorrerDePaginaEmPaginaAsync(HttpClient client, Guid processo)
    {
        List<Guid> percorridos = [];
        string? url = $"/api/publicacoes/entidades/{EntidadesDeRegistroPeloDominio.ProcessoSeletivo}/{processo}/atos?limit=1";

        while (url is not null)
        {
            HttpResponseMessage pagina = await ListarPaginaAsync(client, url);
            percorridos.AddRange(await IdsDaPaginaAsync(pagina));
            url = AtoNormativoApoio.LinkDe(pagina, "next");
        }

        return percorridos;
    }

    private static async Task<HttpResponseMessage> ListarPaginaAsync(HttpClient client, string url)
    {
        HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return resposta;
    }

    private static async Task<Guid[]> IdsDaPaginaAsync(HttpResponseMessage pagina)
    {
        using JsonDocument corpo = JsonDocument.Parse(await pagina.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }
}
