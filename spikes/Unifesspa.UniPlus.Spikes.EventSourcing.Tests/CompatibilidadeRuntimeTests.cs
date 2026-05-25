using AwesomeAssertions;
using JasperFx.Events;
using Marten;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate T0 (continuidade): prova de runtime de que Marten 8.37.1 + WolverineFx.Marten
/// 5.39.3 funcionam sob .NET 10 com o driver Npgsql 10.0.2 de produção — append no
/// stream e projeção inline materializada. Se esta suíte passa, a combinação de
/// versões é viável e o spike pode prosseguir.
/// </summary>
[Collection(ColecaoSpike.Nome)]
public sealed class CompatibilidadeRuntimeTests(SpikeFixture fixture)
{
    [Fact(DisplayName = "T0: append no stream e projeção inline funcionam sob .NET 10 + Npgsql 10")]
    public async Task AbrirEdital_anexa_evento_e_materializa_read_model()
    {
        // Arrange
        Guid editalId = Guid.CreateVersion7();
        Ator ator = new(Guid.CreateVersion7(), "Servidor de Teste", "00000000191");

        // Act: invoca o handler pela middleware transacional do Wolverine
        await fixture.Bus.InvokeAsync(
            new AbrirEdital(editalId, "001/2026", "Edital de teste do spike", ator));

        // Assert: a projeção single-stream inline foi materializada no mesmo commit
        await using IQuerySession session = fixture.Store.QuerySession();
        EditalEs? view = await session.LoadAsync<EditalEs>(editalId);

        view.Should().NotBeNull();
        view!.Status.Should().Be(StatusEditalEs.Rascunho);
        view.NumeroEdital.Should().Be("001/2026");
        view.Titulo.Should().Be("Edital de teste do spike");
    }

    [Fact(DisplayName = "T0: os eventos crus são persistidos no event store do Marten")]
    public async Task AbrirEdital_persiste_evento_no_stream()
    {
        // Arrange
        Guid editalId = Guid.CreateVersion7();
        Ator ator = new(Guid.CreateVersion7(), "Servidor de Teste", "00000000191");

        // Act
        await fixture.Bus.InvokeAsync(
            new AbrirEdital(editalId, "002/2026", "Outro edital do spike", ator));

        // Assert: o stream tem exatamente 1 evento
        await using IQuerySession session = fixture.Store.QuerySession();
        IReadOnlyList<IEvent> eventos = await session.Events.FetchStreamAsync(editalId);

        eventos.Should().ContainSingle();
    }
}
