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
/// Depois da permissão na unidade, a criação do processo devolve juntas todas as recusas que não dependem umas das
/// outras, na ordem em que quem cria as corrige, com o código principal da primeira. O identificador já usado, quando
/// é a única recusa, continua sendo o conflito (409).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CriacaoDoProcessoRecusasAcumuladasEndpointTests : IAsyncLifetime
{
    private const string Prefixo = "uniplus.selecao.processo_seletivo.";

    private static readonly (string Campo, string? Valor)[] SemLocalidade =
        [("localidadeCodigoIbge", null), ("localidadeNome", null), ("localidadeUf", null)];

    private readonly CascadingFixture _fixture;

    public CriacaoDoProcessoRecusasAcumuladasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() =>
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Identificador fora do formato, localidade não declarada e tipo inexistente saem juntos, na ordem, e nada é gravado")]
    public async Task IdentificadorLocalidadeETipo_SaemJuntosNaOrdem()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearComPermissaoAsync();
        string corpo = Alterar(
            Corpo(tipoProcessoId, unidadeId), [("identificadorLegivel", "PSIQ-2026"), ("tipoProcessoOrigemId", Guid.CreateVersion7().ToString()), .. SemLocalidade]);
        int antes = await TotalDeProcessosAsync();

        (HttpStatusCode status, JsonElement problema) = await EnviarAsync(corpo);

        status.Should().Be(HttpStatusCode.UnprocessableEntity, problema.ToString());
        Erros(problema).Should().Equal(
            ("identificadorLegivel", Prefixo + "identificador_legivel_formato_invalido"),
            ("localidadeCodigoIbge", Prefixo + "localidade_ausente"),
            ("tipoProcessoOrigemId", Prefixo + "tipo_processo_nao_encontrado_ou_inativo"));
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "identificador_legivel_formato_invalido");
        (await TotalDeProcessosAsync()).Should().Be(antes, "nada é gravado na recusa");
    }

    [Fact(DisplayName = "Identificador já usado e localidade não declarada saem juntos com 422, com o uso por último")]
    public async Task IdentificadorEmUsoELocalidade_UsoPorUltimo()
    {
        (Guid unidadeId, Guid tipoProcessoId, string identificador) = await CriarProcessoExistenteAsync();
        int antes = await TotalDeProcessosAsync();

        (HttpStatusCode status, JsonElement problema) = await EnviarAsync(
            Alterar(Corpo(tipoProcessoId, unidadeId), [("identificadorLegivel", identificador), .. SemLocalidade]));

        status.Should().Be(HttpStatusCode.UnprocessableEntity, problema.ToString());
        Erros(problema).Should().Equal(
            ("localidadeCodigoIbge", Prefixo + "localidade_ausente"),
            ("identificadorLegivel", Prefixo + "identificador_legivel_em_uso"));
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "localidade_ausente");
        problema.ToString().Should().NotContain(identificador, "nenhuma mensagem repete o valor recebido");
        (await TotalDeProcessosAsync()).Should().Be(antes, "nada é gravado na recusa");
    }

    [Fact(DisplayName = "Identificador já usado como única recusa continua sendo o conflito 409")]
    public async Task IdentificadorEmUsoSozinho_Conflito409()
    {
        (Guid unidadeId, Guid tipoProcessoId, string identificador) = await CriarProcessoExistenteAsync();
        int antes = await TotalDeProcessosAsync();

        (HttpStatusCode status, JsonElement problema) = await EnviarAsync(Alterar(Corpo(tipoProcessoId, unidadeId), [("identificadorLegivel", identificador)]));

        status.Should().Be(HttpStatusCode.Conflict, problema.ToString());
        problema.GetProperty("code").GetString().Should().Be(Prefixo + "identificador_legivel_em_uso");
        (await TotalDeProcessosAsync()).Should().Be(antes, "nada é gravado na recusa");
    }

    private async Task<(Guid UnidadeId, Guid TipoProcessoId)> SemearComPermissaoAsync()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearAsync(_fixture);
        await ConcederPermissaoNaUnidadeAsync(_fixture, unidadeId);
        return (unidadeId, tipoProcessoId);
    }

    private async Task<(Guid UnidadeId, Guid TipoProcessoId, string Identificador)> CriarProcessoExistenteAsync()
    {
        (Guid unidadeId, Guid tipoProcessoId) = await SemearComPermissaoAsync();
        string corpo = Corpo(tipoProcessoId, unidadeId);
        (HttpStatusCode status, JsonElement _) = await EnviarAsync(corpo);
        status.Should().Be(HttpStatusCode.Created);
        return (unidadeId, tipoProcessoId, JsonDocument.Parse(corpo).RootElement.GetProperty("identificadorLegivel").GetString()!);
    }

    private async Task<(HttpStatusCode Status, JsonElement Problema)> EnviarAsync(string corpo)
    {
        using HttpClient client = ClienteDe(_fixture);
        using HttpResponseMessage resposta = await PostAsync(client, corpo);
        string texto = await resposta.Content.ReadAsStringAsync();

        return (resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private static (string? Campo, string? Codigo)[] Erros(JsonElement problema) =>
        [.. problema.GetProperty("errors").EnumerateArray()
            .Select(static erro => (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()))];

    /// <summary>Troca o valor de cada campo do corpo; valor nulo tira o campo do pedido.</summary>
    private static string Alterar(string corpo, IEnumerable<(string Campo, string? Valor)> alteracoes)
    {
        using JsonDocument documento = JsonDocument.Parse(corpo);
        Dictionary<string, JsonElement> campos = documento.RootElement.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.Clone());
        foreach ((string campo, string? valor) in alteracoes)
        {
            campos.Remove(campo);
            if (valor is not null)
            {
                campos[campo] = JsonSerializer.SerializeToElement(valor);
            }
        }

        return JsonSerializer.Serialize(campos);
    }

    private async Task<int> TotalDeProcessosAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.Set<ProcessoSeletivo>().CountAsync();
    }
}
