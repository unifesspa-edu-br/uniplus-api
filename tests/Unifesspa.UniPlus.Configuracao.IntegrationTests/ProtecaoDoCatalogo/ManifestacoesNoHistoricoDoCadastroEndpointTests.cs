namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O administrador do cadastro lê, no histórico do item que administra, as manifestações técnicas de todos os ciclos,
/// com os textos e as substituídas, sem poder se manifestar nem decidir (UNI-REQ-0205, UNI-REQ-0373, UNI-REQ-0374).
/// Marlene só mantém os tipos de documento; Sérgio só consulta os fatos do candidato.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ManifestacoesNoHistoricoDoCadastroEndpointTests : ManifestacaoDaProtecaoTestBase
{
    private const string MediaType = "application/vnd.uniplus.protecao-do-catalogo-manifestacao.v1+json";

    private static readonly Persona Marlene = new("marlene.martins", "Marlene Márcia Emily Martins");
    private static readonly Persona Sergio = new("sergio.dias", "Sérgio Theo Dias");

    public ManifestacoesNoHistoricoDoCadastroEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Quem mantém os tipos de documento lê as manifestações do tipo, com o texto e a substituída, e não as do fato")]
    public async Task MantenedorDeTipos_LeOsDoTipo()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        (Guid tipo, Guid fato) = await DoisItensManifestadosAsync(client);

        List<JsonElement> doTipo = await LerAsync(client, Marlene, $"{Tipos}/{tipo}/protecao/manifestacoes");
        HttpResponseMessage doFato = await Enviar(client, HttpMethod.Get, $"{Fatos}/{fato}/protecao/manifestacoes", Marlene);

        doTipo.Select(static m => (m.GetProperty("situacao").GetString(), m.GetProperty("texto").GetString())).Should().Equal(
            ("SUBSTITUIDA", "O laudo revela condição de saúde."), ("VIGENTE", "Reitero: o dado é sensível."));
        doFato.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Quem consulta os fatos do candidato lê as manifestações do fato, e não as do tipo")]
    public async Task ConsultorDeFatos_LeOsDoFato()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        (Guid tipo, Guid fato) = await DoisItensManifestadosAsync(client);

        List<JsonElement> doFato = await LerAsync(client, Sergio, $"{Fatos}/{fato}/protecao/manifestacoes");
        HttpResponseMessage doTipo = await Enviar(client, HttpMethod.Get, $"{Tipos}/{tipo}/protecao/manifestacoes", Sergio);

        doFato.Should().ContainSingle().Which.GetProperty("autorNome").GetString().Should().Be(Kevin.Nome);
        doTipo.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Sem a permissão do cadastro nem a de consultar a proteção, a leitura é recusada com 403; o item inexistente responde 404")]
    public async Task SemPermissao_403_EInexistente_404()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        (Guid tipo, _) = await DoisItensManifestadosAsync(client);

        HttpResponseMessage representante = await Enviar(client, HttpMethod.Get, $"{Tipos}/{tipo}/protecao/manifestacoes", Betina);
        HttpResponseMessage inexistente = await Enviar(client, HttpMethod.Get, $"{Tipos}/{Guid.NewGuid()}/protecao/manifestacoes", Marlene);

        (representante.StatusCode, inexistente.StatusCode).Should().Be((HttpStatusCode.Forbidden, HttpStatusCode.NotFound));
    }

    [Fact(DisplayName = "O Encarregado lê pelas rotas do cadastro com a permissão de consultar a proteção, e a leitura é registrada")]
    public async Task Encarregado_LeERegistra()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        (Guid tipo, Guid fato) = await DoisItensManifestadosAsync(client);
        int antes = Registro.Registrados.Count;

        await LerAsync(client, Lucia, $"{Tipos}/{tipo}/protecao/manifestacoes");
        await LerAsync(client, Lucia, $"{Fatos}/{fato}/protecao/manifestacoes");

        Registro.Registrados.Skip(antes).Where(r => r.AtorSubject == Lucia.Sub && r.Permitido)
            .Select(static r => r.Permissao).Should().HaveCount(2).And.OnlyContain(static p => p == "configuracao:protecao-do-catalogo:consultar");
    }

    [Fact(DisplayName = "O Representante do controlador e o Encarregado continuam lendo pelo endpoint da proteção")]
    public async Task EndpointDaProtecao_ContinuaServindo()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        (Guid tipo, _) = await DoisItensManifestadosAsync(client);

        (await HistoricoAsync(client, Betina, "TIPO_DOCUMENTO", tipo)).Should().HaveCount(2);
        (await HistoricoAsync(client, Lucia, "TIPO_DOCUMENTO", tipo)).Should().HaveCount(2);
    }

    // Um tipo com a manifestação de Kevin substituída por outra dele, e um fato com uma manifestação de Kevin.
    private async Task<(Guid Tipo, Guid Fato)> DoisItensManifestadosAsync(HttpClient client)
    {
        await PrepararTodosAsync();
        await ConcederAsync(Marlene, PermissaoConfiguracao.TiposDocumentoManter);
        await ConcederAsync(Sergio, PermissaoConfiguracao.FatosCandidatoConsultar);
        (Guid tipo, Guid fato) = (await SolicitadoAsync(client, "TIPO_DOCUMENTO"), await SolicitadoAsync(client, "FATO"));
        (await ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", tipo, "INADEQUADA", "O laudo revela condição de saúde.")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", tipo, "INADEQUADA", "Reitero: o dado é sensível.")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ManifestarAsync(client, Kevin, "FATO", fato, "ADEQUADA", texto: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return (tipo, fato);
    }

    private async Task ConcederAsync(Persona quem, PermissaoConfiguracao permissao)
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, quem.Sub);
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, quem.Sub, permissao.Codigo);
    }

    private static async Task<List<JsonElement>> LerAsync(HttpClient client, Persona quem, string rota)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, rota, quem);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(MediaType);
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();
}
