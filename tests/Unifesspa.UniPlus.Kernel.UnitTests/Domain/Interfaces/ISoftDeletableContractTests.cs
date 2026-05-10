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

        contrato.MarkAsDeleted("auditor@exemplo.com");

        contrato.IsDeleted.Should().BeTrue();
        contrato.DeletedBy.Should().Be("auditor@exemplo.com");
        contrato.DeletedAt.Should().NotBeNull();
    }

    private sealed class DummySoftDeletable : ISoftDeletable
    {
        public bool IsDeleted { get; private set; }
        public DateTimeOffset? DeletedAt { get; private set; }
        public string? DeletedBy { get; private set; }

        public void MarkAsDeleted(string deletedBy)
        {
            IsDeleted = true;
            DeletedAt = DateTimeOffset.UtcNow;
            DeletedBy = deletedBy;
        }
    }
}
