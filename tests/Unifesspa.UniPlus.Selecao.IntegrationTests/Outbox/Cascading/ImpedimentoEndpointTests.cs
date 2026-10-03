namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O campo da inscrição cuja resposta impede a inscrição, pelo HTTP, contra o Postgres
/// (UNI-REQ-0145): o PUT grava a condição e a mensagem ao candidato, o GET as devolve, e a mensagem
/// ausente é recusada no campo dela.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ImpedimentoEndpointTests
{
    private const string Mensagem = "Quem é quilombola concorre pelo processo próprio.";

    private readonly CascadingFixture _fixture;

    public ImpedimentoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O PUT dos itens da inscrição grava o impedimento, e o GET o devolve com a mensagem")]
    public async Task DefinirItens_ComImpedimento_GravaEDevolve()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage gravado = await DefinirItensAsync(client, processoId, Mensagem);
        gravado.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravado.Content.ReadAsStringAsync());

        using HttpRequestMessage get = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        get.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        HttpResponseMessage lido = await client.SendAsync(get);
        lido.StatusCode.Should().Be(HttpStatusCode.OK, await lido.Content.ReadAsStringAsync());

        using JsonDocument corpo = JsonDocument.Parse(await lido.Content.ReadAsStringAsync());
        JsonElement impedimento = corpo.RootElement.GetProperty("formularios").EnumerateArray()
            .Single(static f => f.GetProperty("finalidade").GetString() == "INSCRICAO")
            .GetProperty("fatosColetados").EnumerateArray()
            .Single(static f => f.GetProperty("fatoCodigo").GetString() == "QUILOMBOLA")
            .GetProperty("impedimento");
        impedimento.GetProperty("mensagem").GetString().Should().Be(Mensagem);
        JsonElement condicao = impedimento.GetProperty("quando").EnumerateArray().Single().EnumerateArray().Single();
        condicao.GetProperty("fato").GetString().Should().Be("QUILOMBOLA");
        condicao.GetProperty("valor").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "O impedimento sem mensagem é recusado no campo da mensagem")]
    public async Task DefinirItens_ImpedimentoSemMensagem_Recusa()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage recusado = await DefinirItensAsync(client, processoId, mensagem: null);

        recusado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await recusado.Content.ReadAsStringAsync();
        corpo.Should().Contain("itens[0].impedimento.mensagem").And.Contain("uniplus.item_formulario.impedimento_mensagem_invalida");
    }

    private static async Task<HttpResponseMessage> DefinirItensAsync(HttpClient client, Guid processoId, string? mensagem)
    {
        using HttpRequestMessage put = Requisicao(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens");
        put.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        put.Content = JsonContent.Create(new
        {
            itens = new[]
            {
                new
                {
                    fatoCodigo = "QUILOMBOLA",
                    ordem = FormularioDeTeste.PrimeiraOrdemDeInscricao,
                    rotulo = "Quilombola",
                    tipoRenderizacao = "BOOLEANO",
                    obrigatoriedade = "SEMPRE",
                    etapaCodigo = FormularioDeTeste.Secao,
                    impedimento = new { quando = new[] { new[] { new { fato = "QUILOMBOLA", operador = "IGUAL", valor = true } } }, mensagem },
                },
            },
            grupos = Array.Empty<object>(),
        });
        return await client.SendAsync(put);
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Impedimento {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string rota)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }
}
