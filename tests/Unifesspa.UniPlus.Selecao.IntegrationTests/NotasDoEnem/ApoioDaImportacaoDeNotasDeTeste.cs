namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net.Http.Headers;
using System.Text.Json;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// O que as classes de teste da importação de notas do ENEM têm em comum: o certame publicado com inscritos, o
/// formulário e a requisição autenticada, o envio do arquivo e a leitura da resposta.
/// </summary>
internal sealed class ApoioDaImportacaoDeNotasDeTeste
{
    public static readonly IReadOnlyList<PersonasDeTeste.PessoaFicticia> Pessoas = PersonasDeTeste.Candidatas;

    private readonly CascadingFixture _fixture;

    public ApoioDaImportacaoDeNotasDeTeste(CascadingFixture fixture) => _fixture = fixture;

    public sealed record Cenario(Publicado Processo, IReadOnlyDictionary<int, Guid> Inscricoes)
    {
        public string Base => $"/api/selecao/processos-seletivos/{Processo.ProcessoId}/etapas/{Processo.EtapaDoEnemId}/edicoes-do-enem/{Edicao}/lotes-de-notas";
    }

    public Task<Cenario> NovoCenarioAsync(params int[] inscritas) => NovoCenarioAsync(inscritas, semSegundaOpcao: []);

    public async Task<Cenario> NovoCenarioAsync(int[] inscritas, int[] semSegundaOpcao, DateTimeOffset? fimDasInscricoes = null)
    {
        Publicado publicado = await SemearEPublicarAsync(_fixture, fimDasInscricoes);
        Dictionary<int, Guid> inscricoes = await ImportacaoDeNotasDeTeste.InscreverAsync(_fixture, publicado, inscritas, semSegundaOpcao);

        return new Cenario(publicado, inscricoes);
    }

    public static MultipartFormDataContent Formulario(
        byte[] arquivo, string tipo = "text/plain", string leiaute = "INEP_ENEM_RETORNO", string versao = "v1")
    {
        MultipartFormDataContent formulario = new();
        ByteArrayContent conteudo = new(arquivo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        formulario.Add(conteudo, "arquivo", "retorno.txt");
        formulario.Add(new StringContent(leiaute), "leiauteCodigo");
        formulario.Add(new StringContent(versao), "leiauteVersao");
        return formulario;
    }

    public static HttpRequestMessage Requisicao(HttpMethod metodo, string url, HttpContent? corpo = null, string? papel = "plataforma-admin")
    {
        HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative)) { Content = corpo };
        if (papel is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        return request;
    }

    public static async Task<HttpResponseMessage> ImportarAsync(HttpClient client, Cenario cenario, byte[] arquivo, string tipo = "text/plain")
    {
        using MultipartFormDataContent formulario = Formulario(arquivo, tipo);
        using HttpRequestMessage request = Requisicao(HttpMethod.Post, cenario.Base, formulario);
        return await client.SendAsync(request);
    }

    public static async Task<JsonDocument> LerAsync(HttpResponseMessage resposta) =>
        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

    public static async Task<string?> CodigoAsync(HttpResponseMessage resposta)
    {
        using JsonDocument doc = await LerAsync(resposta);
        return doc.RootElement.GetProperty("code").GetString();
    }

    public static async Task<int> QuantidadeDeLotesAsync(HttpClient client, Cenario cenario)
    {
        using HttpRequestMessage request = Requisicao(HttpMethod.Get, cenario.Base);
        using JsonDocument doc = await LerAsync(await client.SendAsync(request));
        return doc.RootElement.GetArrayLength();
    }

    public static async Task<Guid> IdDoLoteAsync(HttpResponseMessage resposta)
    {
        using JsonDocument doc = await LerAsync(resposta);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    public static byte[] Arquivo(int pessoa) => Bytes([Linha(Pessoas[pessoa], pessoa + 1)]);
}
