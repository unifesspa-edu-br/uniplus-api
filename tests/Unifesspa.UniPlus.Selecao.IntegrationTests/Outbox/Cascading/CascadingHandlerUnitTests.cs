namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Unit test puro do handler cascading: exercita
// <see cref="PublicarEditalCascadingHandler.Handle"/> com EF Core InMemory,
// sem Wolverine, sem fixture, sem container. Asserta o contrato cascading
// (retorno como <c>IEnumerable&lt;object&gt;</c> contendo o
// <c>EditalPublicadoEvent</c> emitido pelo agregado) e o invariante
// canônico da ADR-0005: <c>DequeueDomainEvents</c> esvazia a coleção do
// agregado no mesmo ponto da drenagem.
//
// Trait dedicado <c>OutboxCascadingUnit</c> separa este do conjunto de
// testes de capacidade outbox (que exigem container Postgres).
[Trait("Category", "OutboxCascadingUnit")]
public sealed class CascadingHandlerUnitTests
{
    [Fact(DisplayName = "Handler cascading retorna EditalPublicadoEvent emitido pelo agregado, sem Wolverine no caminho")]
    public async Task Handler_RetornaCascadingComEditalPublicadoEvent()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseInMemoryDatabase($"cascading-unit-{Guid.NewGuid():N}")
            .Options;

        await using SelecaoDbContext db = new(options);

        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(numero: 1, ano: 2026);
        numeroResult.IsSuccess.Should().BeTrue();
        NumeroEdital numero = numeroResult.Value!;

        var command = new PublicarEditalCascadingCommand(
            numero,
            "UnitTest cascading",
            TipoProcesso.SiSU);

        IEnumerable<object> cascading = await PublicarEditalCascadingHandler.Handle(
            command,
            db,
            CancellationToken.None);

        IReadOnlyList<object> materialized = [.. cascading];
        materialized.Should().ContainSingle(e => e is EditalPublicadoEvent,
            "o agregado emite EditalPublicadoEvent ao chamar Publicar() e o handler retorna a coleção de domain events como cascading messages");

        EditalPublicadoEvent published = (EditalPublicadoEvent)materialized
            .Single(e => e is EditalPublicadoEvent);
        published.NumeroEdital.Should().Be(numero.ToString());
        published.EditalId.Should().NotBeEmpty();

        Edital? persistido = await db.Editais.FirstOrDefaultAsync(e => e.Id == published.EditalId);
        persistido.Should().NotBeNull(
            "SaveChangesAsync foi chamado dentro do handler — entidade deve estar materializada no contexto InMemory");
        persistido!.DomainEvents.Should().BeEmpty(
            "padrão canônico do ADR-0005: handler usa DequeueDomainEvents() para esvaziar a coleção do agregado no mesmo ponto da drenagem");
    }
}
