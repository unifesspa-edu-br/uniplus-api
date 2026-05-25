using AwesomeAssertions;
using JasperFx.Events;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G2 host-level — passo 2: no host único, o handler EF Core (store 'main') e o
/// handler Marten ancillary commitam cada um atomicamente com a entrega pelo outbox,
/// sem promessa de transação cross-store.
/// </summary>
[Collection(ColecaoCoexistencia.Nome)]
public sealed class CoexistenciaTests(CoexistenciaFixture fixture)
{
    [Fact(DisplayName = "G2: handler EF Core (main) escreve a entidade E entrega pelo outbox")]
    public async Task Handler_ef_escreve_e_entrega()
    {
        Guid id = Guid.CreateVersion7();

        await fixture.Bus.InvokeAsync(new CriarRegistroCrud(id, "registro de coabitação"));

        // Escrita EF Core commitada
        using IServiceScope escopo = fixture.Host.Services.CreateScope();
        CrudDbContext db = escopo.ServiceProvider.GetRequiredService<CrudDbContext>();
        RegistroCrud? escrito = await db.Registros.FindAsync(id);
        escrito.Should().NotBeNull("a entidade EF Core deve ser persistida");

        // Entrega pelo outbox 'main'
        ColetorCoexistencia coletor = fixture.Host.Services.GetRequiredService<ColetorCoexistencia>();
        bool entregue = await TestHelpers.EsperarAsync(() => coletor.Contem(id), TimeSpan.FromSeconds(15));
        entregue.Should().BeTrue("o evento de integração do módulo CRUD deve ser entregue pelo outbox");
    }

    [Fact(DisplayName = "G2: o event store ancillary (Marten) coabita e é utilizável no mesmo host")]
    public async Task Event_store_ancillary_coabita_e_e_utilizavel()
    {
        Guid streamId = Guid.CreateVersion7();

        // Append direto pela sessão do store ANCILLARY, no mesmo host do outbox EF Core 'main'.
        await using (IDocumentSession sessao = fixture.StoreEs.LightweightSession())
        {
            sessao.Events.StartStream(streamId,
                new FatoEsAnexado(streamId, "fato event-sourced", DateTimeOffset.UtcNow));
            await sessao.SaveChangesAsync();
        }

        // O fato persiste no event store ancillary, em schema próprio.
        await using IQuerySession consulta = fixture.StoreEs.QuerySession();
        IReadOnlyList<IEvent> eventos = await consulta.Events.FetchStreamAsync(streamId);
        eventos.Should().ContainSingle("o evento deve ser anexado ao stream do store ancillary");
    }
}
