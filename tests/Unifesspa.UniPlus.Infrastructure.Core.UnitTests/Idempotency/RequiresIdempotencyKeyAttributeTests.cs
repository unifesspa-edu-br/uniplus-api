namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Idempotency;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

public sealed class RequiresIdempotencyKeyAttributeTests
{
    [Fact(DisplayName = "TtlSeconds default é o sentinel -1 (usa IdempotencyOptions.Ttl)")]
    public void TtlSeconds_SemOverride_UsaSentinel()
    {
        RequiresIdempotencyKeyAttribute atributo = new();

        atributo.TtlSeconds.Should().Be(-1);
    }

    [Fact(DisplayName = "TtlSeconds aceita override explícito via inicializador de objeto")]
    public void TtlSeconds_ComOverride_PreservaValor()
    {
        RequiresIdempotencyKeyAttribute atributo = new() { TtlSeconds = 900 };

        atributo.TtlSeconds.Should().Be(900);
    }
}
