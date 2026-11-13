namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O apoio dos testes da manifestação técnica na proteção do catálogo de tipos de documento e de fatos do candidato
/// (ADR-0163): além de Isabella, que mantém os cadastros, e de Betina e Caroline, Representantes do controlador, Kevin
/// e Lúcia são Encarregados de dados.
/// </summary>
public abstract class ManifestacaoDaProtecaoTestBase : HistoricoDaProtecaoTestBase
{
    private protected static readonly Persona Kevin = new("kevin.carvalho", "Kevin Anderson Carvalho");
    private protected static readonly Persona Lucia = new("lucia.almeida", "Lúcia Bruna Maya Almeida");

    private protected ManifestacaoDaProtecaoTestBase(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>A rota do cadastro no detalhe e no histórico da proteção.</summary>
    private protected static string Rota(string cadastro) => cadastro == "FATO" ? "fatos" : "tipos-documento";

    /// <summary>A classificação com que os itens destes testes nascem: o fato pessoal, o tipo interno.</summary>
    private protected static string Classificacao(string cadastro) => cadastro == "FATO" ? "PESSOAL" : "INTERNO";

    private protected async Task PrepararTodosAsync()
    {
        await PrepararPersonasAsync();
        foreach (Persona encarregado in new[] { Kevin, Lucia })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, encarregado.Sub);
            await ConcessoesDeTeste.ConcederGovernancaAsync(
                Fixture.Factory.Services, encarregado.Sub, TipoDeGovernanca.EncarregadoDeDados, $"{encarregado.Sub}@unifesspa.edu.br");
        }
    }

    /// <summary>Um item novo do cadastro, já numa solicitação de Isabella.</summary>
    private protected static async Task<Guid> SolicitadoAsync(HttpClient client, string cadastro)
    {
        Guid id = cadastro == "FATO"
            ? await CadastrarFatoAsync(client, $"{Marca()} Renda familiar bruta")
            : await CadastrarTipoAsync(client, $"{Marca()} Laudo médico complementar");
        (await SolicitarAsync(client, Isabella, cadastro, id)).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private protected static Task<HttpResponseMessage> ManifestarAsync(
        HttpClient client, Persona quem, string cadastro, Guid id, string parecer, string? texto = "A classificação deve ser sensível.", int? ciclo = 1) =>
        Enviar(client, HttpMethod.Post, $"{Base}/manifestacoes", quem, new { cadastro, itemId = id, ciclo, parecer, texto });

    private protected static Task<HttpResponseMessage> DecidirComoAsync(
        HttpClient client, Persona quem, string cadastro, Guid id, string decisao, string? justificativa = null, string? recomendada = null) =>
        DecidirAsync(client, quem, new(cadastro, id, decisao, Classificacao(cadastro), recomendada, justificativa));

    /// <summary>A decisão de Betina, Representante do controlador, sobre o que ela leu antes, e não sobre o item de agora.</summary>
    private protected static Task<HttpResponseMessage> DecidirSobreALeituraAsync(HttpClient client, Decisao decisao, LeituraDoCircuitoLida lida) =>
        DecidirAsync(client, Betina, decisao with { Lida = lida });

    private protected static Task<HttpResponseMessage> AceitarAsync(HttpClient client, string cadastro, Guid id) =>
        Enviar(client, HttpMethod.Post, $"{(cadastro == "FATO" ? Fatos : Tipos)}/{id}/aceite-da-recomendacao", Isabella);

    private protected static async Task<JsonElement> DetalheAsync(HttpClient client, string cadastro, Guid id, Persona? quem = null)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/{Rota(cadastro)}/{id}", quem ?? Betina);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private protected static async Task<List<JsonElement>> HistoricoAsync(HttpClient client, Persona quem, string cadastro, Guid id)
    {
        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Base}/{Rota(cadastro)}/{id}/manifestacoes", quem);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.protecao-do-catalogo-manifestacao.v1+json");
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private protected static async Task<string> CodigoAsync(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

    /// <summary>O último ato do item, como o detalhe o mostra.</summary>
    private protected static async Task<JsonElement> UltimoAtoAsync(HttpClient client, string cadastro, Guid id) =>
        (await DetalheAsync(client, cadastro, id)).GetProperty("atos").EnumerateArray().Last();
}
