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
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// O token que a leitura devolve é o mesmo que a escrita aceita (issue #1294): grava,
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
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();
        string sufixo = Guid.CreateVersion7().ToString("N")[..8];

        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync(api, sufixo);

        HttpResponseMessage criacao = await CriarAsync(
            client, $"PS ida e volta {sufixo}", "inscricaoPropria", tipoProcessoId, unidadeId);
        criacao.StatusCode.Should().Be(HttpStatusCode.Created,
            "a criação aceita o token camelCase do contrato");
        Guid processoId = await criacao.Content.ReadFromJsonAsync<Guid>();

        JsonElement lido = await ObterJsonCruAsync(client, processoId);

        string origemLida = lido.GetProperty("origemCandidatos").GetString()!;
        origemLida.Should().Be("inscricaoPropria",
            "a leitura devolve o vocabulário da escrita — antes devolvia 'InscricaoPropria', "
            + "que nenhuma opção do formulário casava");
        lido.GetProperty("status").GetString().Should().Be("rascunho");

        // A prova do ida e volta: o valor que acabou de ser lido volta pela escrita sem tradução.
        HttpResponseMessage recriacao = await CriarAsync(
            client, $"PS ida e volta {sufixo} reenvio", origemLida, tipoProcessoId, unidadeId);
        recriacao.StatusCode.Should().Be(HttpStatusCode.Created,
            "o token que a leitura devolveu ('{0}') tem de ser aceito de volta pela criação — é o "
            + "que o cliente faz ao reler um recurso para reeditá-lo", origemLida);
    }

    private static async Task<HttpResponseMessage> CriarAsync(
        HttpClient client, string nome, string origemCandidatos, Guid tipoProcessoId, Guid unidadeId)
    {
        // Corpo cru para fixar o TEXTO do token que trafega — serializar o enum deixaria a
        // grafia a cargo do serializador do teste, e é justamente ela que está sob prova.
        string corpo = $$"""
            {
              "nome": "{{nome}}",
              "tipoProcessoOrigemId": "{{tipoProcessoId}}",
              "origemCandidatos": "{{origemCandidatos}}",
              "unidadeAdministradoraOrigemId": "{{unidadeId}}",
              "localidadeCodigoIbge": "1504208",
              "localidadeNome": "Marabá",
              "localidadeUf": "PA"
            }
            """;

        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }

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
        Guid unidadeId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            OrganizacaoInstitucionalDbContext org = scope.ServiceProvider
                .GetRequiredService<OrganizacaoInstitucionalDbContext>();
            Unidade unidade = Unidade.Criar(
                nome: "Centro de Processos Seletivos — vocabulário",
                alias: null,
                slug: $"ceps-vocab-{sufixo}",
                sigla: $"CEPSV{sufixo}",
                codigo: $"CEPSV-{sufixo}",
                unidadeSuperiorId: null,
                tipo: TipoUnidade.Centro,
                unidadeAcademica: true,
                vigenciaInicio: new DateOnly(2020, 1, 1),
                vigenciaFim: null,
                // A Unidade administradora sem cidade recusa a criação do processo (issue #1114).
                cidadeCodigoIbge: "1504208",
                cidadeNome: "Marabá",
                cidadeUf: "PA").Value!;
            org.Unidades.Add(unidade);
            await org.SaveChangesAsync().ConfigureAwait(false);
            unidadeId = unidade.Id;
        }

        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext config = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            Guid tipoProcessoId = await config.TiposProcesso
                .Where(tipo => tipo.Codigo == "SiSU" && tipo.Ativo)
                .Select(tipo => tipo.Id)
                .SingleAsync().ConfigureAwait(false);
            return (unidadeId, tipoProcessoId);
        }
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
