namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Interfaces;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public sealed class IAuditableEntityContractTests
{
    private static readonly DateTimeOffset InstanteDeCriacao = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "IAuditableEntity expõe CreatedAt, UpdatedAt, CreatedBy, UpdatedBy via implementação dummy")]
    public void DummyImplementacao_ExpoePropriedades()
    {
        DateTimeOffset agora = InstanteDeCriacao;
        IAuditableEntity contrato = CriarAuditavel(agora);

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
            CreatedAt = InstanteDeCriacao,
            UpdatedAt = null,
            CreatedBy = null,
            UpdatedBy = null,
        };

        IAuditableEntity contrato = dummy;

        contrato.UpdatedAt.Should().BeNull();
        contrato.CreatedBy.Should().BeNull();
        contrato.UpdatedBy.Should().BeNull();
    }

    private static DummyAuditavel CriarAuditavel(DateTimeOffset criadoEm) => new()
    {
        CreatedAt = criadoEm,
        UpdatedAt = criadoEm.AddMinutes(5),
        CreatedBy = "alice",
        UpdatedBy = "bob",
    };

    private sealed class DummyAuditavel : IAuditableEntity
    {
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
        public string? CreatedBy { get; init; }
        public string? UpdatedBy { get; init; }
    }
}
