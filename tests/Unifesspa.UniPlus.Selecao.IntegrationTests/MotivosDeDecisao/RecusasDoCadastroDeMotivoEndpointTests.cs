namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Recusas de campo do cadastro único de motivos de decisão e do filtro da listagem pelo endpoint
/// real: o payload do problema traz o código, o título e a mensagem do contrato, e nenhuma
/// mensagem devolve o valor recusado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class RecusasDoCadastroDeMotivoEndpointTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/admin/motivos-de-decisao";
    private const string Prefixo = "uniplus.selecao.motivo_de_decisao.";
    private const string Leandro = "leandro.viana";
    private const string PrefixoDeTeste = "TEST_MDR_";
    private const string TipoRecusado = "RECURSO_DA_BANCA";

    private readonly CascadingFixture _fixture;

    public RecusasDoCadastroDeMotivoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);
    }

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        await db.Database.ExecuteSqlAsync(
            $"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE 'TEST\\_MDR\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "POST sem tipo é recusado como tipo obrigatório, com a lista de tipos aceitos")]
    public async Task Criar_SemTipo_RecusaComoObrigatorio()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(client, Payload(tipo: null));

        JsonElement problema = await LerProblemaAsync(resposta);
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "tipo_obrigatorio");
        problema.GetProperty("title").GetString().Should().Be("Tipo do motivo é obrigatório");
        problema.GetProperty("detail").GetString().Should()
            .Be($"Informe o tipo do motivo. Tipos aceitos: {TipoDoMotivoInformado.RotulosAceitosEmTexto}.");
        CampoDoErro(problema).Should().Be("tipo");
    }

    [Fact(DisplayName = "POST com tipo fora do vocabulário é recusado como tipo desconhecido, sem repetir o valor")]
    public async Task Criar_TipoForaDoVocabulario_RecusaComoInvalido()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(client, Payload(tipo: TipoRecusado));

        JsonElement problema = await LerProblemaAsync(resposta);
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "tipo_invalido");
        problema.GetProperty("title").GetString().Should().Be("Tipo de decisão desconhecido");
        CampoDoErro(problema).Should().Be("tipo");
        ConferirSemOValorRecusado(problema, TipoRecusado);
    }

    [Theory(DisplayName = "O filtro recusa tipo fora do vocabulário com erro próprio, sem repetir o valor")]
    [InlineData(TipoRecusado)]
    [InlineData("NENHUM")]
    [InlineData(" ISENCAO_DA_TAXA ")]
    [InlineData("isencao_da_taxa")]
    public async Task Listar_TipoForaDoVocabulario_RecusaComoInvalido(string tipo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/selecao/motivos-de-decisao?tipo={tipo}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "filtro_de_tipo_invalido");
        problema.GetProperty("title").GetString().Should().Be("Tipo do filtro de motivos inválido");
        problema.GetProperty("detail").GetString().Should().NotContain(tipo);
    }

    [Theory(DisplayName = "O filtro recusa subclassificação fora do vocabulário com erro próprio")]
    [InlineData("cadastro_unico")]
    [InlineData(" CADASTRO_UNICO ")]
    [InlineData("   ")]
    public async Task Listar_SubclassificacaoForaDoVocabulario_Recusa(string subclassificacao)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/selecao/motivos-de-decisao?subclassificacao={Uri.EscapeDataString(subclassificacao)}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "filtro_de_subclassificacao_invalida");
        problema.GetProperty("detail").GetString().Should().NotContain(subclassificacao);
    }

    [Fact(DisplayName = "POST sem sentido é recusado com 422")]
    public async Task Criar_SemSentido_Recusa()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(client, Payload(sentido: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com subclassificação em tipo que não a define é recusado com o código nomeado, sem repetir o valor")]
    public async Task Criar_SubclassificacaoForaDaIsencao_Recusa()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(
            client, Payload("HOMOLOGACAO_DAS_INSCRICOES", "SUBCLASSIFICACAO_RECUSADA"));

        JsonElement problema = await LerProblemaAsync(resposta);
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "subclassificacao_nao_admitida");
        ConferirSemOValorRecusado(problema, "SUBCLASSIFICACAO_RECUSADA");
    }

    [Fact(DisplayName = "Descrição com caractere nulo é recusada pelo domínio, não pelo banco")]
    public async Task Criar_DescricaoComCaractereNulo_Recusa()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(client, Payload(descricao: "Renda\u0000acima do limite."));

        // O Postgres não armazena U+0000 em coluna textual. Sem recusa no agregado, o valor
        // atravessa a validação e estoura no SaveChanges, devolvendo 500 para um payload só inválido.
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static async Task<JsonElement> LerProblemaAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        return JsonDocument.Parse(corpo).RootElement.Clone();
    }

    private static string? CampoDoErro(JsonElement problema) =>
        problema.GetProperty("errors").EnumerateArray().First().GetProperty("field").GetString();

    private static void ConferirSemOValorRecusado(JsonElement problema, string valor)
    {
        problema.GetProperty("detail").GetString().Should().NotContain(valor);
        problema.GetProperty("errors").EnumerateArray()
            .Select(erro => erro.GetProperty("message").GetString())
            .Should().OnlyContain(mensagem => !mensagem!.Contains(valor, StringComparison.Ordinal));
    }

    private static object Payload(
        string? tipo = "ISENCAO_DA_TAXA",
        string? subclassificacao = "CADASTRO_UNICO",
        string? sentido = "DESFAVORAVEL",
        string descricao = "Renda familiar per capita acima do limite legal.") => new
        {
            tipo,
            codigo = $"{PrefixoDeTeste}{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            descricao,
            subclassificacao,
            sentido,
        };

    private async Task<HttpClient> ClienteComConcessaoAsync()
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            _fixture.Factory.Services, Leandro, PermissaoSelecao.MotivosDeDecisaoManter.Codigo);
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Leandro);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Admin Test");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@unifesspa.edu.br");
        return client;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, object payload)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
