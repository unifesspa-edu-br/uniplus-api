using AwesomeAssertions;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G1: o append no Event Store (Marten) e o envelope de integração no outbox
/// (Wolverine) commitam na mesma transação — ou ambos, ou nenhum.
/// </summary>
[Collection(ColecaoSpike.Nome)]
public sealed class AtomicidadeTests(SpikeFixture fixture)
{
    [Fact(DisplayName = "G1 happy: publicar anexa o evento E entrega a mensagem de integração pelo outbox")]
    public async Task Publicar_anexa_evento_e_entrega_integracao()
    {
        // Arrange: um edital aberto
        Guid editalId = Guid.CreateVersion7();
        await fixture.Bus.InvokeAsync(
            new AbrirEdital(editalId, "010/2026", "Edital G1 happy", TestHelpers.AtorFicticio()));

        // Act: publica
        await fixture.Bus.InvokeAsync(
            new PublicarEdital(editalId, "hash-config-v1", TestHelpers.AtorFicticio()));

        // Assert: o append foi commitado (stream com 2 eventos, projeção Publicado)
        await using IQuerySession session = fixture.Store.QuerySession();
        IReadOnlyList<IEvent> eventos = await session.Events.FetchStreamAsync(editalId);
        eventos.Should().HaveCount(2);

        EditalEs? view = await session.LoadAsync<EditalEs>(editalId);
        view!.Status.Should().Be(StatusEditalEs.Publicado);

        // Assert: o envelope do outbox foi entregue após o commit
        ColetorIntegracao coletor = fixture.Host.Services.GetRequiredService<ColetorIntegracao>();
        bool entregue = await TestHelpers.EsperarAsync(
            () => coletor.Contem(editalId), TimeSpan.FromSeconds(15));
        entregue.Should().BeTrue("o evento de integração deve ser entregue pelo outbox após o commit do append");
    }

    [Fact(DisplayName = "G1 rollback: falha após anexar não commita nem o evento nem o envelope do outbox")]
    public async Task Falha_apos_anexar_nao_commita_evento_nem_integracao()
    {
        // Arrange: um edital aberto (1 evento)
        Guid editalId = Guid.CreateVersion7();
        await fixture.Bus.InvokeAsync(
            new AbrirEdital(editalId, "011/2026", "Edital G1 rollback", TestHelpers.AtorFicticio()));

        ColetorIntegracao coletor = fixture.Host.Services.GetRequiredService<ColetorIntegracao>();

        // Act: comando que anexa + publica e então lança
        Func<Task> acao = () => fixture.Bus.InvokeAsync(
            new FalharAposAnexar(editalId, "motivo qualquer", TestHelpers.AtorFicticio()));

        // Assert: a invocação propaga a exceção
        await acao.Should().ThrowAsync<InvalidOperationException>();

        // Assert: o stream continua com apenas o evento de abertura (append revertido)
        await using IQuerySession session = fixture.Store.QuerySession();
        IReadOnlyList<IEvent> eventos = await session.Events.FetchStreamAsync(editalId);
        eventos.Should().ContainSingle("o append deve ser revertido junto com a transação");

        // Assert: a mensagem de integração NÃO é entregue (envelope nunca commitado).
        // Espera uma janela para garantir que não há entrega tardia.
        bool entregueIndevidamente = await TestHelpers.EsperarAsync(
            () => coletor.Contem(editalId), TimeSpan.FromSeconds(3));
        entregueIndevidamente.Should().BeFalse("o envelope do outbox deve ser revertido junto com o append");
    }
}
