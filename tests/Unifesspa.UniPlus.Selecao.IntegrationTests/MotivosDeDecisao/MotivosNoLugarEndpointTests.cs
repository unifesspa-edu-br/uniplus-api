namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao.ChamadasDosMotivosDeTeste;

/// <summary>
/// A escolha dos motivos numa fase ou numa etapa pelo endpoint real, contra Postgres via Testcontainers
/// (UNI-REQ-0285): o mesmo motivo vale em lugares diferentes, cada lugar o escolhe uma vez, e o lugar que o processo não
/// tem é recusado antes de qualquer outra coisa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class MotivosNoLugarEndpointTests : IAsyncLifetime
{
    private readonly ChamadasDosMotivosDeTeste _chamadas;

    public MotivosNoLugarEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _chamadas = new ChamadasDosMotivosDeTeste(fixture, "TEST_MNL");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _chamadas.LimparAsync();

    [Fact(DisplayName = "O mesmo motivo é escolhido na fase e na etapa, a leitura traz os dois lugares, e repetir o mesmo lugar é recusado")]
    public async Task MesmoMotivo_EmLugaresDiferentes_RepetirNoLugarRecusa()
    {
        (Guid processoId, _) = await _chamadas.SemearProcessoAsync(nameof(MesmoMotivo_EmLugaresDiferentes_RepetirNoLugarRecusa));
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        (string naFase, string naEtapa) = await RotasDoMotivoNosDoisLugaresAsync(processoId, motivo.Id);

        (await _chamadas.IncluirAsync(naFase)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _chamadas.IncluirAsync(naEtapa)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage repetida = await _chamadas.IncluirAsync(naEtapa);

        IReadOnlyList<JsonElement> escolhidos = await _chamadas.LerAsync(processoId);
        escolhidos.Should().HaveCount(2);
        escolhidos.Count(e => e.GetProperty("lugar").GetProperty("faseCanonicaOrigemId").ValueKind == JsonValueKind.String).Should().Be(1);
        escolhidos.Count(e => e.GetProperty("lugar").GetProperty("etapaId").ValueKind == JsonValueKind.String).Should().Be(1);
        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodigoDoErroAsync(repetida)).Should().Be("uniplus.selecao.motivo_de_decisao.ja_escolhido");
    }

    [Fact(DisplayName = "Retirar da etapa remove só a escolha dela, e retirar o que o lugar não escolheu devolve não escolhido")]
    public async Task Retirar_RemoveSoDoLugar_NaoEscolhidoRecusa()
    {
        (Guid processoId, _) = await _chamadas.SemearProcessoAsync(nameof(Retirar_RemoveSoDoLugar_NaoEscolhidoRecusa));
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        (string naFase, string naEtapa) = await RotasDoMotivoNosDoisLugaresAsync(processoId, motivo.Id);
        (await _chamadas.IncluirAsync(naFase)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage naoEscolhido = await _chamadas.RetirarAsync(naEtapa);
        (await _chamadas.IncluirAsync(naEtapa)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage retirada = await _chamadas.RetirarAsync(naEtapa);

        naoEscolhido.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoDoErroAsync(naoEscolhido)).Should().Be("uniplus.selecao.motivo_de_decisao.nao_escolhido");
        retirada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _chamadas.LerAsync(processoId)).Should().ContainSingle().Which.GetProperty("lugar").GetProperty("etapaId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "A fase ou a etapa que o processo não tem, inclusive a identidade vazia, devolve fase ou etapa não encontrada")]
    public async Task LugarInexistente_NaoEncontrado()
    {
        (Guid processoId, _) = await _chamadas.SemearProcessoAsync(nameof(LugarInexistente_NaoEncontrado));
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();

        foreach (string rota in RotasDeLugarInexistente(processoId, motivo.Id))
        {
            await ConferirNaoEncontradoAsync(rota);
        }
    }

    private static string[] RotasDeLugarInexistente(Guid processoId, Guid motivoId) =>
    [
        LugaresDoProcessoDeTeste.RotaDaFase(processoId, Guid.CreateVersion7(), motivoId),
        LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, Guid.CreateVersion7(), motivoId),
        LugaresDoProcessoDeTeste.RotaDaFase(processoId, Guid.Empty, motivoId),
        LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, Guid.Empty, motivoId),
    ];

    private async Task<(string NaFase, string NaEtapa)> RotasDoMotivoNosDoisLugaresAsync(Guid processoId, Guid motivoId) =>
    (
        LugaresDoProcessoDeTeste.RotaDaFase(processoId, await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId), motivoId),
        LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId), motivoId));

    private async Task ConferirNaoEncontradoAsync(string rota)
    {
        using HttpResponseMessage inclusao = await _chamadas.IncluirAsync(rota);
        using HttpResponseMessage retirada = await _chamadas.RetirarAsync(rota);

        inclusao.StatusCode.Should().Be(HttpStatusCode.NotFound, rota);
        (await CodigoDoErroAsync(inclusao)).Should().Be("uniplus.selecao.motivo_de_decisao.fase_ou_etapa_nao_encontrada");
        retirada.StatusCode.Should().Be(HttpStatusCode.NotFound, rota);
        (await CodigoDoErroAsync(retirada)).Should().Be("uniplus.selecao.motivo_de_decisao.fase_ou_etapa_nao_encontrada");
    }
}
