using AwesomeAssertions;
using JasperFx.Events.Daemon;
using Marten;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G4: replay do stream reconstrói o estado do agregado e o rebuild da projeção
/// a partir do Event Store reproduz o mesmo read model materializado inline.
/// </summary>
[Collection(ColecaoSpike.Nome)]
public sealed class ReplayProjecaoTests(SpikeFixture fixture)
{
    [Fact(DisplayName = "G4: replay reconstrói o estado e bate com a projeção inline")]
    public async Task Replay_reconstroi_estado_igual_a_projecao_inline()
    {
        // Arrange: abrir, publicar e retificar duas vezes
        Guid editalId = Guid.CreateVersion7();
        await fixture.Bus.InvokeAsync(
            new AbrirEdital(editalId, "020/2026", "Edital G4", TestHelpers.AtorFicticio()));
        await fixture.Bus.InvokeAsync(
            new PublicarEdital(editalId, "hash-config-v1", TestHelpers.AtorFicticio()));
        await fixture.Bus.InvokeAsync(
            new RetificarEdital(editalId, "ajuste de cronograma", TestHelpers.AtorFicticio()));
        await fixture.Bus.InvokeAsync(
            new RetificarEdital(editalId, "correção de vagas", TestHelpers.AtorFicticio()));

        await using IQuerySession session = fixture.Store.QuerySession();

        // Act: replay puro (live aggregation) a partir dos eventos
        EditalEs? porReplay = await session.Events.AggregateStreamAsync<EditalEs>(editalId);

        // Projeção inline materializada
        EditalEs? inline = await session.LoadAsync<EditalEs>(editalId);

        // Assert: estado reconstruído correto e idêntico ao read model inline
        porReplay.Should().NotBeNull();
        porReplay!.Status.Should().Be(StatusEditalEs.Publicado);
        porReplay.QuantidadeRetificacoes.Should().Be(2);
        porReplay.MotivoUltimaRetificacao.Should().Be("correção de vagas");

        inline.Should().BeEquivalentTo(porReplay);
    }

    [Fact(DisplayName = "G4: rebuild da projeção a partir do Event Store reproduz o read model")]
    public async Task Rebuild_da_projecao_reproduz_read_model()
    {
        // Arrange
        Guid editalId = Guid.CreateVersion7();
        await fixture.Bus.InvokeAsync(
            new AbrirEdital(editalId, "021/2026", "Edital G4 rebuild", TestHelpers.AtorFicticio()));
        await fixture.Bus.InvokeAsync(
            new PublicarEdital(editalId, "hash-config-v1", TestHelpers.AtorFicticio()));

        // Act: rebuild completo da projeção EditalEs a partir dos eventos (via daemon)
        using IProjectionDaemon daemon = await fixture.Store.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync<EditalEs>(CancellationToken.None);

        // Assert: o read model reconstruído reflete o estado publicado
        await using IQuerySession session = fixture.Store.QuerySession();
        EditalEs? reconstruido = await session.LoadAsync<EditalEs>(editalId);

        reconstruido.Should().NotBeNull();
        reconstruido!.Status.Should().Be(StatusEditalEs.Publicado);
        reconstruido.NumeroEdital.Should().Be("021/2026");
    }
}
