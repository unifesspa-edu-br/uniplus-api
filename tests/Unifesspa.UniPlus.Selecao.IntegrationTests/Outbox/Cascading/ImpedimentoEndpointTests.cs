namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
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

        using JsonDocument corpo = await ProcessoSeletivoHttp.ObterAsync(client, processoId);
        JsonElement quilombola = FormularioNaRespostaDoProcesso.Item(FormularioNaRespostaDoProcesso.DeInscricao(corpo), "QUILOMBOLA");
        ConferirImpedimentoDoQuilombola(quilombola.GetProperty("regras").GetProperty("impedimento"));
    }

    [Fact(DisplayName = "O impedimento sem mensagem é recusado no campo da mensagem")]
    public async Task DefinirItens_ImpedimentoSemMensagem_Recusa()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage recusado = await DefinirItensAsync(client, processoId, mensagem: null);

        recusado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await recusado.Content.ReadAsStringAsync();
        corpo.Should().Contain("itens[0].regras.impedimento.mensagem").And.Contain("uniplus.item_formulario.impedimento_mensagem_invalida");
    }

    private static void ConferirImpedimentoDoQuilombola(JsonElement impedimento)
    {
        impedimento.GetProperty("mensagem").GetString().Should().Be(Mensagem);
        JsonElement condicao = impedimento.GetProperty("quando").EnumerateArray().Single().EnumerateArray().Single();
        condicao.GetProperty("fato").GetString().Should().Be("QUILOMBOLA");
        condicao.GetProperty("valor").GetBoolean().Should().BeTrue();
    }

    private static Task<HttpResponseMessage> DefinirItensAsync(HttpClient client, Guid processoId, string? mensagem) =>
        ProcessoSeletivoHttp.DefinirItensAsync(client, processoId, new { itens = new[] { QuilombolaComImpedimento(mensagem) }, grupos = Array.Empty<object>() });

    private static object QuilombolaComImpedimento(string? mensagem) => new
    {
        fatoCodigo = "QUILOMBOLA",
        ordem = FormularioDeTeste.PrimeiraOrdemDeInscricao,
        etapaCodigo = FormularioDeTeste.Secao,
        apresentacao = new { rotulo = "Quilombola", tipoDeCampo = "SIM_OU_NAO" },
        regras = new
        {
            obrigatoriedade = new { tipo = "SEMPRE" },
            impedimento = new { quando = new[] { new[] { new { fato = "QUILOMBOLA", operador = "IGUAL", valor = true } } }, mensagem },
        },
    };

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Impedimento {Guid.CreateVersion7()}" });
        return processo.Id;
    }
}
