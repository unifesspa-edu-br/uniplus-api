namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O token que a leitura devolve é o mesmo que a escrita aceita: grava,
/// relê, e reenvia o valor lido numa nova criação — pelo HTTP real, ponta a ponta.
/// </summary>
/// <remarks>
/// <para>
/// O reenvio é o que fecha a prova. Conferir a string devolvida diz que ela mudou; só
/// reenviá-la mostra que ela é aceita de volta — que é o que o consumidor faz ao reler um
/// recurso para reeditá-lo, e o que falhava antes: a criação recebia <c>inscricaoPropria</c>
/// e o GET devolvia <c>InscricaoPropria</c>, sem nada no contrato declarando a diferença.
/// </para>
/// <para>
/// A leitura é sobre o JSON cru, nunca sobre o DTO desserializado: o conversor de enum é
/// tolerante a caixa, então ler de volta no tipo aceitaria as duas grafias e o teste
/// passaria com o defeito presente.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class VocabularioIdaEVoltaEndpointTests
{
    private const string Rota = "/api/selecao/processos-seletivos";

    private readonly CascadingFixture _fixture;

    public VocabularioIdaEVoltaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Criar com o token, reler o mesmo token, e criar de novo com o valor lido")]
    public async Task CriarLerRecriar_MesmoVocabularioNasTresPontas()
    {
        HttpClient client = _fixture.Factory.CreateClient();
        string sufixo = Guid.CreateVersion7().ToString("N")[^8..];
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync(_fixture.Factory, sufixo);
        HttpResponseMessage criacao = await CriarAsync(
            client, $"PS ida e volta {sufixo}", "INSCRICAO_PROPRIA", tipoProcessoId, unidadeId);
        criacao.StatusCode.Should().Be(HttpStatusCode.Created, "a criação aceita o token camelCase do contrato");
        Guid processoId = await criacao.Content.ReadFromJsonAsync<Guid>();
        JsonElement lido = await ObterJsonCruAsync(client, processoId);
        string origemLida = ConferirVocabularioLido(lido);
        HttpResponseMessage recriacao = await CriarAsync(
            client, $"PS ida e volta {sufixo} reenvio", origemLida, tipoProcessoId, unidadeId);
        recriacao.StatusCode.Should().Be(HttpStatusCode.Created,
            "o token que a leitura devolveu ('{0}') tem de ser aceito de volta pela criação", origemLida);
    }

    private static string ConferirVocabularioLido(JsonElement lido)
    {
        string origemLida = lido.GetProperty("origemCandidatos").GetProperty("codigo").GetString()!;
        origemLida.Should().Be("INSCRICAO_PROPRIA", "a leitura devolve o vocabulário da escrita");
        lido.GetProperty("situacao").GetProperty("codigo").GetString().Should().Be("RASCUNHO");
        return origemLida;
    }

    private static async Task<HttpResponseMessage> CriarAsync(
        HttpClient client, string nome, string origemCandidatos, Guid tipoProcessoId, Guid unidadeId)
    {
        string corpo = CorpoDaCriacao(nome, origemCandidatos, tipoProcessoId, unidadeId);
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string CorpoDaCriacao(string nome, string origemCandidatos, Guid tipoProcessoId, Guid unidadeId) =>
        $$"""
        {
          "nome": "{{nome}}",
          "tipoProcessoOrigemId": "{{tipoProcessoId}}",
          "origemCandidatos": "{{origemCandidatos}}",
          "unidadeAdministradoraOrigemId": "{{unidadeId}}",
          "localidadeCodigoIbge": "1504208",
          "localidadeNome": "Marabá",
          "localidadeUf": "PA",
          "identificadorLegivel": "{{IdentificadoresDeTeste.NovoValor()}}"
        }
        """;

    private static async Task<JsonElement> ObterJsonCruAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get, new Uri($"{Rota}/{processoId}", UriKind.Relative));
        Autenticar(request);
        using HttpResponseMessage resposta = await client.SendAsync(request).ConfigureAwait(false);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument documento = JsonDocument.Parse(
            await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
        return documento.RootElement.Clone();
    }

    private static async Task<(Guid UnidadeId, Guid TipoProcessoId)> SemearAsync(
        CascadingApiFactory api, string sufixo)
    {
        Guid unidadeId = await SemearUnidadeAsync(api, sufixo);
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext config = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid tipoProcessoId = await config.TiposProcesso
            .Where(tipo => tipo.Codigo == "SISU" && tipo.Ativo)
            .Select(tipo => tipo.Id)
            .SingleAsync().ConfigureAwait(false);
        return (unidadeId, tipoProcessoId);
    }

    private static async Task<Guid> SemearUnidadeAsync(CascadingApiFactory api, string sufixo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext org = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = CriarUnidade(sufixo);
        org.Unidades.Add(unidade);
        await org.SaveChangesAsync().ConfigureAwait(false);
        return unidade.Id;
    }

    private static Unidade CriarUnidade(string sufixo) => Unidade.Criar(
        "Centro de Processos Seletivos — vocabulário",
        new IdentificadoresDaUnidade($"ceps-vocab-{sufixo}", $"CEPSV{sufixo}", $"CEPSV-{sufixo}", null),
        new CidadeDaUnidade("1504208", "Marabá", "PA"),
        new VigenciaDaUnidade(new DateOnly(2020, 1, 1), null),
        null, TipoUnidade.Centro, true).Value!;

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
