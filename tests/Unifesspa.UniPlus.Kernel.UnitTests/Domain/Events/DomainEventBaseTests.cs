namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Events;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Events;

public sealed class DomainEventBaseTests
{
    [Fact(DisplayName = "DomainEventBase atribui EventId UUID v7 distinto por instância")]
    public void EventId_EhUuidV7Unico()
    {
        EventoDeTeste a = new("a");
        EventoDeTeste b = new("b");

        a.EventId.Should().NotBe(b.EventId);
        ExtrairVersao(a.EventId).Should().Be(7,
            "DomainEventBase usa Guid.CreateVersion7 para ordering temporal no outbox (ADR-0032)");
        ExtrairVersao(b.EventId).Should().Be(7);
    }

    [Fact(DisplayName = "DomainEventBase atribui OccurredOn em UTC próximo de UtcNow")]
    public void OccurredOn_ProximoUtcNow()
    {
        DateTimeOffset antes = DateTimeOffset.UtcNow;

        EventoDeTeste evento = new("x");

        DateTimeOffset depois = DateTimeOffset.UtcNow;

        evento.OccurredOn.Should().BeOnOrAfter(antes.AddSeconds(-5))
            .And.BeOnOrBefore(depois.AddSeconds(5));
        evento.OccurredOn.Offset.Should().Be(TimeSpan.Zero,
            "OccurredOn é normalizado em UTC para serialização determinística");
    }

    [Fact(DisplayName = "DomainEventBase implementa IDomainEvent")]
    public void Implementa_IDomainEvent()
    {
        EventoDeTeste evento = new("x");

        evento.Should().BeAssignableTo<IDomainEvent>();
    }

    [Fact(DisplayName = "Records de domain event não colidem por payload — EventId e OccurredOn entram na igualdade record-based")]
    public void Records_Igualdade_ConsideraEventIdEOccurredOn()
    {
        EventoDeTeste a = new("mesmo-payload");
        EventoDeTeste b = new("mesmo-payload");

        // Records do C# comparam todos os campos sintetizados — EventId e
        // OccurredOn são auto-properties com inicializador, então entram
        // na igualdade. Resultado: dois eventos com mesmo payload mas
        // gerados em momentos distintos são naturalmente desiguais.
        // Propriedade desejável para deduplicação no outbox.
        a.Should().NotBe(b);
        a.EventId.Should().NotBe(b.EventId);
    }

    private static int ExtrairVersao(Guid id)
    {
        byte[] bytes = id.ToByteArray();
        return (bytes[7] & 0xF0) >> 4;
    }

    private sealed record EventoDeTeste(string Payload) : DomainEventBase;
}
