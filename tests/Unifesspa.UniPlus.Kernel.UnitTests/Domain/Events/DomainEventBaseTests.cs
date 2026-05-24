namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Events;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Events;

public sealed class DomainEventBaseTests
{
    private static readonly DateTimeOffset InstanteFixo = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "DomainEventBase atribui EventId UUID v7 distinto por instância")]
    public void EventId_EhUuidV7Unico()
    {
        EventoDeTeste a = new("a", InstanteFixo);
        EventoDeTeste b = new("b", InstanteFixo);

        a.EventId.Should().NotBe(b.EventId);
        ExtrairVersao(a.EventId).Should().Be(7,
            "DomainEventBase usa Guid.CreateVersion7 para ordering temporal no outbox (ADR-0032)");
        ExtrairVersao(b.EventId).Should().Be(7);
    }

    [Fact(DisplayName = "DomainEventBase registra o OccurredOn recebido no construtor (determinístico)")]
    public void OccurredOn_VemDoConstrutor()
    {
        // Convenção de relógio: OccurredOn é parâmetro obrigatório provido por
        // quem levanta o evento (a partir do TimeProvider), nunca DateTimeOffset.UtcNow.
        EventoDeTeste evento = new("x", InstanteFixo);

        evento.OccurredOn.Should().Be(InstanteFixo);
        evento.OccurredOn.Offset.Should().Be(TimeSpan.Zero,
            "OccurredOn é normalizado em UTC para serialização determinística");
    }

    [Fact(DisplayName = "DomainEventBase implementa IDomainEvent")]
    public void Implementa_IDomainEvent()
    {
        EventoDeTeste evento = new("x", InstanteFixo);

        evento.Should().BeAssignableTo<IDomainEvent>();
    }

    [Fact(DisplayName = "Records de domain event não colidem por payload — EventId e OccurredOn entram na igualdade record-based")]
    public void Records_Igualdade_ConsideraEventIdEOccurredOn()
    {
        EventoDeTeste a = new("mesmo-payload", InstanteFixo);
        EventoDeTeste b = new("mesmo-payload", InstanteFixo);

        // Records do C# comparam todos os campos sintetizados. Mesmo com payload
        // e OccurredOn idênticos, o EventId (UUID v7 gerado por instância) difere,
        // tornando os dois eventos desiguais — propriedade desejável para
        // deduplicação no outbox.
        a.Should().NotBe(b);
        a.EventId.Should().NotBe(b.EventId);
    }

    private static int ExtrairVersao(Guid id)
    {
        byte[] bytes = id.ToByteArray();
        return (bytes[7] & 0xF0) >> 4;
    }

    private sealed record EventoDeTeste(string Payload, DateTimeOffset OccurredOn) : DomainEventBase(OccurredOn);
}
