namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Interfaces;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

// Contract test via dummy implementation. Padrão acordado em #129
// (FakeUnitOfWork): se o contrato público da interface mudar, o dummy
// quebra a compilação e o time vê a breaking change imediatamente.
public sealed class IAuditableEntityContractTests
{
    [Fact(DisplayName = "IAuditableEntity expõe CreatedAt, UpdatedAt, CreatedBy, UpdatedBy via implementação dummy")]
    public void DummyImplementacao_ExpoePropriedades()
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        DummyAuditavel dummy = new()
        {
            CreatedAt = agora,
            UpdatedAt = agora.AddMinutes(5),
            CreatedBy = "alice",
            UpdatedBy = "bob",
        };

        IAuditableEntity contrato = dummy;

        contrato.CreatedAt.Should().Be(agora);
        contrato.UpdatedAt.Should().Be(agora.AddMinutes(5));
        contrato.CreatedBy.Should().Be("alice");
        contrato.UpdatedBy.Should().Be("bob");
    }

    [Fact(DisplayName = "IAuditableEntity aceita UpdatedAt/CreatedBy/UpdatedBy nulos no contrato")]
    public void Propriedades_OpcionaisAceitamNull()
    {
        DummyAuditavel dummy = new()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = null,
            CreatedBy = null,
            UpdatedBy = null,
        };

        IAuditableEntity contrato = dummy;

        contrato.UpdatedAt.Should().BeNull();
        contrato.CreatedBy.Should().BeNull();
        contrato.UpdatedBy.Should().BeNull();
    }

    private sealed class DummyAuditavel : IAuditableEntity
    {
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
        public string? CreatedBy { get; init; }
        public string? UpdatedBy { get; init; }
    }
}
