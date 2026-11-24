namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Net;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;


/// <summary>
/// A etapa ou a fase que sai do processo leva junto os motivos escolhidos nela (UNI-REQ-0285), pelos endpoints reais
/// contra Postgres via Testcontainers.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class MotivosDosLugaresQueSaemEndpointTests : IAsyncLifetime
{
    private static readonly Guid TipoProvaObjetiva = new("019fee1e-7000-7000-8000-000000000001");

    private readonly CascadingFixture _fixture;
    private readonly ChamadasDosMotivosDeTeste _chamadas;

    public MotivosDosLugaresQueSaemEndpointTests(CascadingFixture ambienteDeTeste)
    {
        ArgumentNullException.ThrowIfNull(ambienteDeTeste);

        _fixture = ambienteDeTeste;
        _chamadas = new ChamadasDosMotivosDeTeste(ambienteDeTeste, "TEST_MLQ");
    }

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public Task DisposeAsync() => _chamadas.LimparAsync();

    [Fact(DisplayName = "No rascunho, substituir a etapa sem devolver o id leva o motivo dela; devolvendo o id, o motivo continua")]
    public async Task Rascunho_EtapaSemId_LevaOMotivo_ComId_Mantem()
    {
        (Guid processoId, _) = await _chamadas.SemearProcessoAsync(nameof(Rascunho_EtapaSemId_LevaOMotivo_ComId_Mantem));
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        Guid etapa = await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, etapa, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage comId = await SubstituirEtapaAsync(processoId, etapa);
        comId.StatusCode.Should().Be(HttpStatusCode.NoContent, await comId.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().ContainSingle("a etapa devolvida com o id é a mesma, e leva o motivo consigo");

        using HttpResponseMessage semId = await SubstituirEtapaAsync(processoId, id: null);
        semId.StatusCode.Should().Be(HttpStatusCode.NoContent, await semId.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().BeEmpty("a etapa nova nasce sem motivo, e o da etapa que saiu foi junto");
    }

    [Fact(DisplayName = "No rascunho, remover a fase do cronograma leva os motivos escolhidos nela e nas etapas dela")]
    public async Task Rascunho_FaseQueSai_LevaOsMotivosDaFaseEDasEtapas()
    {
        (Guid processoId, _) = await SemearProcessoComFaseQueSaiAsync(nameof(Rascunho_FaseQueSai_LevaOsMotivosDaFaseEDasEtapas));
        MotivoDeDecisao motivo = await _chamadas.SemearMotivoAsync();
        Guid fase = await LugaresDoProcessoDeTeste.FaseAsync(_chamadas.Services, processoId, ProcessoComFaseQueSaiDeTeste.CodigoDaFaseQueSai);
        Guid etapa = await LugaresDoProcessoDeTeste.EtapaAsync(_chamadas.Services, processoId);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaFase(processoId, fase, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _chamadas.IncluirAsync(LugaresDoProcessoDeTeste.RotaDaEtapa(processoId, etapa, motivo.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage substituido = await SubstituirCronogramaAsync(processoId);

        substituido.StatusCode.Should().Be(HttpStatusCode.NoContent, await substituido.Content.ReadAsStringAsync());
        (await _chamadas.LerAsync(processoId)).Should().BeEmpty("a fase saiu, e a etapa dela com ela");
    }

    private Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoComFaseQueSaiAsync(string nome) =>
        ProcessoComFaseQueSaiDeTeste.SemearAsync(_chamadas.Services, $"{nome} {Guid.CreateVersion7()}");

    /// <summary>Substitui o cronograma só pela fase da inscrição: a fase de avaliação sai, e a etapa dela com ela.</summary>
    private async Task<HttpResponseMessage> SubstituirCronogramaAsync(Guid processoId) =>
        await _chamadas.EnviarAsync(
            HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/cronograma-fases", new[] { await FaseDaInscricaoAsync() });

    private async Task<object> FaseDaInscricaoAsync() => new
    {
        ordem = 1,
        faseCanonicaId = await LugaresDoProcessoDeTeste.FaseDoCatalogoAsync(_chamadas.Services, ProcessoComFaseQueSaiDeTeste.CodigoDaFaseQueFica),
        inicio = new DateTimeOffset(2030, 1, 10, 0, 0, 0, TimeSpan.Zero),
        fim = new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
        produtos = Array.Empty<object>(),
        emiteParecerIndividual = false,
        bancasRequeridas = Array.Empty<object>(),
        regraRecurso = (object?)null,
    };

    private Task<HttpResponseMessage> SubstituirEtapaAsync(Guid processoId, Guid? id) =>
        _chamadas.EnviarAsync(
            HttpMethod.Put,
            $"/api/selecao/processos-seletivos/{processoId}/etapas",
            new[]
            {
                new
                {
                    id, nome = "Prova Objetiva", carater = CaraterEtapa.Classificatoria.Codigo(), tipoEtapaOrigemId = TipoProvaObjetiva, peso = 1m, notaMinima = (decimal?)null, notaMaxima = 100m,
                    formaDeAvaliacao = "OBJETIVA", ordem = 1, faseCodigo = "RESULTADO_FINAL",
                    produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>(),
                },
            });
}
