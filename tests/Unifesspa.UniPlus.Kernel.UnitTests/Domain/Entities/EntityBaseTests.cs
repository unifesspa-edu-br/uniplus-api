namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

// Cobre o ciclo de vida básico do EntityBase. A drenagem de domain events
// fica em EntityBaseDomainEventsTests — esta classe foca em Id, audit e
// soft delete (ADR-0032 + invariantes da issue #127).
public sealed class EntityBaseTests
{
    [Fact(DisplayName = "Nova entidade nasce com Id UUID v7 (preserva ordering temporal)")]
    public void NovaEntidade_TemIdUuidV7()
    {
        EntidadeDeTeste entidade = new();

        entidade.Id.Should().NotBeEmpty();
        ExtrairVersao(entidade.Id).Should().Be(7,
            "EntityBase usa Guid.CreateVersion7 para ganhar ordering temporal (ADR-0032)");
    }

    [Fact(DisplayName = "Duas entidades criadas em sequência têm Ids distintos")]
    public void NovaEntidade_IdEhUnico()
    {
        EntidadeDeTeste a = new();
        EntidadeDeTeste b = new();

        a.Id.Should().NotBe(b.Id);
    }

    [Fact(DisplayName = "CreatedAt é preenchido com UtcNow ±5s no momento da criação")]
    public void CreatedAt_ProximoDoAgoraUtc()
    {
        DateTimeOffset antes = DateTimeOffset.UtcNow;

        EntidadeDeTeste entidade = new();

        DateTimeOffset depois = DateTimeOffset.UtcNow;

        entidade.CreatedAt.Should().BeOnOrAfter(antes.AddSeconds(-5));
        entidade.CreatedAt.Should().BeOnOrBefore(depois.AddSeconds(5));
        entidade.CreatedAt.Offset.Should().Be(TimeSpan.Zero,
            "audit trail é normalizado em UTC");
    }

    [Fact(DisplayName = "Entidade recém-criada não tem UpdatedAt, IsDeleted, DeletedAt, DeletedBy")]
    public void NovaEntidade_FlagsDeAuditNaoIniciam()
    {
        EntidadeDeTeste entidade = new();

        entidade.UpdatedAt.Should().BeNull();
        entidade.IsDeleted.Should().BeFalse();
        entidade.DeletedAt.Should().BeNull();
        entidade.DeletedBy.Should().BeNull();
    }

    [Fact(DisplayName = "MarkAsDeleted altera IsDeleted, DeletedAt (UtcNow) e DeletedBy")]
    public void MarkAsDeleted_AlteraFlagsDeSoftDelete()
    {
        EntidadeDeTeste entidade = new();
        DateTimeOffset antes = DateTimeOffset.UtcNow;

        entidade.MarkAsDeleted("usuario@exemplo.com");

        DateTimeOffset depois = DateTimeOffset.UtcNow;

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedBy.Should().Be("usuario@exemplo.com");
        entidade.DeletedAt.Should().NotBeNull();
        entidade.DeletedAt!.Value.Should().BeOnOrAfter(antes.AddSeconds(-1))
            .And.BeOnOrBefore(depois.AddSeconds(1));
        entidade.DeletedAt!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "MarkAsDeleted pode ser chamado mais de uma vez — sobrescreve DeletedBy")]
    public void MarkAsDeleted_PodeSerChamadoMaisDeUmaVez()
    {
        EntidadeDeTeste entidade = new();
        entidade.MarkAsDeleted("primeiro");

        entidade.MarkAsDeleted("segundo");

        entidade.IsDeleted.Should().BeTrue();
        entidade.DeletedBy.Should().Be("segundo");
    }

    [Fact(DisplayName = "DomainEvents inicia como coleção vazia e imutável (não array)")]
    public void DomainEvents_IniciaVazioEImutavel()
    {
        EntidadeDeTeste entidade = new();

        entidade.DomainEvents.Should().BeEmpty();
        (entidade.DomainEvents is List<Kernel.Domain.Events.IDomainEvent>).Should().BeFalse(
            "exposição interna usa AsReadOnly — caller não consegue castar para List<T>");
    }

    [Fact(DisplayName = "EntityBase usa igualdade referencial — não há override Equals/GetHashCode baseado em Id")]
    public void Igualdade_EhReferencial_NaoBaseadaEmId()
    {
        // EntityBase é classe (não record) e não sobrescreve Equals/GetHashCode.
        // O design atual privilegia identidade gerenciada pelo ChangeTracker do
        // EF Core; agregados são comparados por referência dentro do escopo do
        // DbContext. Issue #16 lista "igualdade por Id" como AC, mas o código
        // não implementa esse contrato hoje — este teste pin documenta o
        // comportamento real e quebrará se algum dia o design mudar.

        EntidadeDeTeste a = new();
        EntidadeDeTeste b = new();

        a.Equals(b).Should().BeFalse("instâncias distintas — igualdade por referência");
        a.Equals(a).Should().BeTrue();
        ReferenceEquals(a, b).Should().BeFalse(
            "duas instâncias têm referências distintas; assertiva direta evita risco teórico de colisão em GetHashCode");
    }

    // RFC 9562 §5.7 — versão fica nos 4 bits altos do byte 6 (offset 7 em string little-endian).
    private static int ExtrairVersao(Guid id)
    {
        byte[] bytes = id.ToByteArray();
        return (bytes[7] & 0xF0) >> 4;
    }

    private sealed class EntidadeDeTeste : EntityBase
    {
    }
}
