namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AnexoDoAtoConfiguration : IEntityTypeConfiguration<AnexoDoAto>
{
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<AnexoDoAto> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "anexo_do_ato",
            t =>
            {
                // Defesa em profundidade contra insert cru; a factory já recusa.
                t.HasCheckConstraint("ck_anexo_do_ato_hash", "hash ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_anexo_do_ato_nome", "btrim(nome) <> ''");
                t.HasCheckConstraint(
                    "ck_anexo_do_ato_tipo_midia",
                    "tipo_midia ~ '^[a-z0-9][a-z0-9.+_-]*/[a-z0-9][a-z0-9.+_-]*$'");
                t.HasCheckConstraint("ck_anexo_do_ato_ordem", "ordem >= 0");
            });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AtoId).IsRequired();
        builder.Property(a => a.Ordem).IsRequired();
        builder.Property(a => a.Nome).HasMaxLength(AnexoDoAto.NomeMaxLength).IsRequired();
        builder.Property(a => a.TipoMidia).HasMaxLength(AnexoDoAto.TipoMidiaMaxLength).IsRequired();
        builder.Property(a => a.Hash).HasMaxLength(HashLength).IsFixedLength().IsRequired();

        // A única chave estrangeira do anexo aponta para o próprio ato. Cascade: o anexo é
        // parte do ato; ambos são append-only, e nenhum DELETE chega até aqui.
        builder.HasOne<AtoNormativo>()
            .WithMany(a => a.Anexos)
            .HasForeignKey(a => a.AtoId)
            .HasConstraintName("fk_anexo_do_ato_ato")
            .OnDelete(DeleteBehavior.Cascade);

        // O nome distingue o anexo dentro do ato, e a posição o ordena na leitura: repetir
        // qualquer um dos dois deixaria o ato com anexos indistinguíveis.
        builder.HasIndex(a => new { a.AtoId, a.Nome })
            .IsUnique()
            .HasDatabaseName("ux_anexo_do_ato_nome");

        builder.HasIndex(a => new { a.AtoId, a.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_anexo_do_ato_ordem");
    }
}
