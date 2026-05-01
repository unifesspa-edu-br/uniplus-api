namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Entities;

using FluentAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Events;

// Cobre o contrato de drenagem de domain events do EntityBase. Foco nos
// invariantes do método DequeueDomainEvents — padrão canônico para
// handlers cascading do Wolverine (ADR-0005).
public sealed class EntityBaseDomainEventsTests
{
    [Fact(DisplayName = "DequeueDomainEvents retorna snapshot e esvazia a coleção do agregado")]
    public void DequeueDomainEvents_RetornaSnapshotEEsvaziaColecao()
    {
        var entidade = new EntidadeDeTeste();
        var evento1 = new EventoDeTeste("e1");
        var evento2 = new EventoDeTeste("e2");

        entidade.AdicionarEvento(evento1);
        entidade.AdicionarEvento(evento2);

        IReadOnlyCollection<IDomainEvent> drenados = entidade.DequeueDomainEvents();

        drenados.Should().HaveCount(2)
            .And.ContainInOrder(evento1, evento2);
        entidade.DomainEvents.Should().BeEmpty(
            "DequeueDomainEvents deve esvaziar a coleção do agregado para evitar republicação acidental");
    }

    [Fact(DisplayName = "DequeueDomainEvents em coleção vazia retorna coleção vazia sem efeito colateral")]
    public void DequeueDomainEvents_ColecaoVazia_RetornaVaziaSemErro()
    {
        var entidade = new EntidadeDeTeste();

        IReadOnlyCollection<IDomainEvent> drenados = entidade.DequeueDomainEvents();

        drenados.Should().BeEmpty();
        entidade.DomainEvents.Should().BeEmpty();
    }

    [Fact(DisplayName = "DequeueDomainEvents é atômico — snapshot não vê eventos adicionados depois")]
    public void DequeueDomainEvents_EhAtomico_NaoVeEventosAdicionadosDepois()
    {
        var entidade = new EntidadeDeTeste();
        var primeiro = new EventoDeTeste("primeiro");
        entidade.AdicionarEvento(primeiro);

        IReadOnlyCollection<IDomainEvent> drenados = entidade.DequeueDomainEvents();

        var posterior = new EventoDeTeste("posterior");
        entidade.AdicionarEvento(posterior);

        drenados.Should().ContainSingle().Which.Should().Be(primeiro,
            "snapshot é tirado antes do Clear; eventos adicionados depois pertencem à próxima drenagem");
        entidade.DomainEvents.Should().ContainSingle().Which.Should().Be(posterior);
    }

    [Fact(DisplayName = "ClearDomainEvents continua zerando a coleção sem retornar snapshot")]
    public void ClearDomainEvents_ZeraColecaoSemRetorno()
    {
        var entidade = new EntidadeDeTeste();
        entidade.AdicionarEvento(new EventoDeTeste("e1"));
        entidade.AdicionarEvento(new EventoDeTeste("e2"));

        entidade.ClearDomainEvents();

        entidade.DomainEvents.Should().BeEmpty();
    }

    private sealed class EntidadeDeTeste : EntityBase
    {
        public void AdicionarEvento(IDomainEvent @event) => AddDomainEvent(@event);
    }

    private sealed record EventoDeTeste(string Identificador) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
    }
}
