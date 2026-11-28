namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDaCriacaoDoProcesso;

/// <summary>
/// A falta de permissão na unidade do pedido vem antes dos dados da criação do processo: quem não pode configurar
/// naquela unidade recebe 403 sem campo algum, qualquer que seja o estado do pedido, e quem pode recebe todas as
/// recusas dos dados juntas (422).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CriacaoDoProcessoPermissaoAntesDosDadosEndpointTests : IAsyncLifetime
{
    private readonly CascadingFixture _fixture;

    public CriacaoDoProcessoPermissaoAntesDosDadosEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() =>
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "Sem permissão na unidade, o pedido com dado inválido é recusado com 403, sem apontar campo nem repetir o valor recebido")]
    [InlineData("nome")]
    [InlineData("tipoProcessoOrigemId")]
    [InlineData("origemCandidatos")]
    public async Task SemPermissao_DadoInvalido_Recusa403SemCampo(string campo)
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync(_fixture);
        string corpo = ComCampoInvalido(Corpo(tipoProcessoId, unidadeId), campo);
        using HttpClient client = ClienteDe(_fixture);
        int antes = await TotalDeProcessosAsync();

        using HttpResponseMessage resposta = await PostAsync(client, corpo);

        string texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden, texto);
        using JsonDocument problema = JsonDocument.Parse(texto);
        problema.RootElement.TryGetProperty("errors", out _).Should().BeFalse("a recusa de acesso não aponta campo do pedido");
        texto.Should().NotContain("valorQueNaoEExibido");
        (await TotalDeProcessosAsync()).Should().Be(antes, "nada é gravado na recusa");
    }

    [Fact(DisplayName = "Sem permissão na unidade, a unidade inexistente também é recusada com 403, sem indicar que ela não existe")]
    public async Task SemPermissao_UnidadeInexistente_Recusa403()
    {
        (_, Guid tipoProcessoId) = await SemearAsync(_fixture);
        using HttpClient client = ClienteDe(_fixture);

        using HttpResponseMessage resposta = await PostAsync(client, Corpo(tipoProcessoId, Guid.CreateVersion7()));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Com permissão na unidade, os dados inválidos saem juntos com 422, cada um no seu campo, e nada é gravado")]
    public async Task ComPermissao_DadosInvalidos_Recusa422ComTodasAsCausas()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync(_fixture);
        await ConcederPermissaoNaUnidadeAsync(_fixture, unidadeId);
        string corpo = ComCampoInvalido(ComCampoInvalido(ComCampoInvalido(Corpo(tipoProcessoId, unidadeId), "nome"), "tipoProcessoOrigemId"), "origemCandidatos");
        using HttpClient client = ClienteDe(_fixture);

        using HttpResponseMessage resposta = await PostAsync(client, corpo);

        string texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, texto);
        using JsonDocument problema = JsonDocument.Parse(texto);
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static erro => erro.GetProperty("field").GetString())
            .Should().Equal("nome", "tipoProcessoOrigemId", "origemCandidatos");
    }

    [Fact(DisplayName = "Com permissão na unidade, o pedido válido cria o processo")]
    public async Task ComPermissao_PedidoValido_Cria()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync(_fixture);
        await ConcederPermissaoNaUnidadeAsync(_fixture, unidadeId);
        using HttpClient client = ClienteDe(_fixture);

        using HttpResponseMessage resposta = await PostAsync(client, Corpo(tipoProcessoId, unidadeId));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Sem a unidade no pedido não há permissão a decidir: a recusa é a dos dados (422) apontando a unidade")]
    public async Task SemUnidade_Recusa422ApontandoAUnidade()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync(_fixture);
        string corpo = Corpo(tipoProcessoId, unidadeId).Replace(unidadeId.ToString(), Guid.Empty.ToString(), StringComparison.Ordinal);
        using HttpClient client = ClienteDe(_fixture);

        using HttpResponseMessage resposta = await PostAsync(client, corpo);

        string texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, texto);
        using JsonDocument problema = JsonDocument.Parse(texto);
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static erro => erro.GetProperty("field").GetString())
            .Should().Contain("unidadeAdministradoraOrigemId");
    }

    private static string ComCampoInvalido(string corpo, string campo)
    {
        using JsonDocument documento = JsonDocument.Parse(corpo);
        Dictionary<string, JsonElement> campos = documento.RootElement.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.Clone());
        campos[campo] = JsonSerializer.SerializeToElement(campo switch
        {
            "nome" => string.Empty,
            "tipoProcessoOrigemId" => Guid.Empty.ToString(),
            _ => "valorQueNaoEExibido",
        });
        return JsonSerializer.Serialize(campos);
    }

    private async Task<int> TotalDeProcessosAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.Set<ProcessoSeletivo>().CountAsync();
    }
}
