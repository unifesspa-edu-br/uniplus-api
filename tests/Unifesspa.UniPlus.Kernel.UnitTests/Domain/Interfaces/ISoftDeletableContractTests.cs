namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Interfaces;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

public sealed class ISoftDeletableContractTests
{
    [Fact(DisplayName = "ISoftDeletable contrato — dummy começa não deletado")]
    public void Dummy_IniciaNaoDeletado()
    {
        DummySoftDeletable dummy = new();
        ISoftDeletable contrato = dummy;

        contrato.IsDeleted.Should().BeFalse();
        contrato.DeletedAt.Should().BeNull();
        contrato.DeletedBy.Should().BeNull();
    }

    [Fact(DisplayName = "ISoftDeletable.MarkAsDeleted muta estado para deletado preservando responsável")]
    public void MarkAsDeleted_AlteraEstado()
    {
        DummySoftDeletable dummy = new();
        ISoftDeletable contrato = dummy;

        DateTimeOffset instante = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        contrato.MarkAsDeleted("auditor@exemplo.com", instante);

        contrato.IsDeleted.Should().BeTrue();
        contrato.DeletedBy.Should().Be("auditor@exemplo.com");
        contrato.DeletedAt.Should().Be(instante);
    }

    private sealed class DummySoftDeletable : ISoftDeletable
    {
        public bool IsDeleted { get; private set; }
        public DateTimeOffset? DeletedAt { get; private set; }
        public string? DeletedBy { get; private set; }

        public void MarkAsDeleted(string deletedBy, DateTimeOffset deletedAt)
        {
            IsDeleted = true;
            DeletedAt = deletedAt;
            DeletedBy = deletedBy;
        }
    }
}
