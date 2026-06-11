namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

// Cobre a capability opt-in de soft-delete (issue #629): SoftDeletableEntity
// carrega a implementação de ISoftDeletable que antes vivia em EntityBase.
// O carimbo automático em SaveChanges é coberto por SoftDeleteInterceptorTests.
public sealed class SoftDeletableEntityTests
{
    [Fact(DisplayName = "SoftDeletableEntity é um EntityBase e implementa ISoftDeletable")]
    public void SoftDeletableEntity_HerdaEntityBase_EImplementaContrato()
    {
        EntidadeDeTeste entidade = new();

        entidade.Should().BeAssignableTo<EntityBase>("soft-delete é opt-in sobre a base de identidade");
        entidade.Should().BeAssignableTo<ISoftDeletable>("o opt-in é detectado pelo interceptor e pela convenção via interface");
        entidade.Id.Should().NotBeEmpty("Id continua vindo de EntityBase");
    }

    [Fact(DisplayName = "Entidade recém-criada não está deletada (IsDeleted/DeletedAt/DeletedBy)")]
    public void NovaEntidade_NaoIniciaDeletada()
    {
        EntidadeDeTeste entidade = new();

        entidade.IsDeleted.Should().BeFalse();
        entidade.DeletedAt.Should().BeNull();
        entidade.DeletedBy.Should().BeNull();
    }

    [Fact(DisplayName = "MarkAsDeleted altera IsDeleted, DeletedAt (instante recebido) e DeletedBy")]
    public void MarkAsDeleted_AlteraFlagsDeSoftDelete()
    {
        EntidadeDeTeste entidade = new();
        // Instante determinístico: o caller (SoftDeleteInterceptor/repositório)
        // provê deletedAt a partir do TimeProvider; o domínio só o registra.
        DateTimeOffset instante = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

        entidade.MarkAsDeleted("usuario@exemplo.com", instante);

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedBy.Should().Be("usuario@exemplo.com");
        entidade.DeletedAt.Should().Be(instante);
        entidade.DeletedAt!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "MarkAsDeleted pode ser chamado mais de uma vez — sobrescreve DeletedBy")]
    public void MarkAsDeleted_PodeSerChamadoMaisDeUmaVez()
    {
        EntidadeDeTeste entidade = new();
        DateTimeOffset instante = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        entidade.MarkAsDeleted("primeiro", instante);

        entidade.MarkAsDeleted("segundo", instante);

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedBy.Should().Be("segundo");
    }

    private sealed class EntidadeDeTeste : SoftDeletableEntity
    {
    }
}
