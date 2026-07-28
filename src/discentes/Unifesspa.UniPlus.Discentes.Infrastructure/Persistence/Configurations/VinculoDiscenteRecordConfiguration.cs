namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class VinculoDiscenteRecordConfiguration : IEntityTypeConfiguration<VinculoDiscenteRecord>
{
    public void Configure(EntityTypeBuilder<VinculoDiscenteRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("vinculo_discente");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Matricula).HasMaxLength(20).IsRequired();

        // Envelope autenticado (nonce + tag + dados cifrados) — nunca os 11 dígitos em
        // texto claro. bytea evita a expansão de ~33% de uma codificação Base64 (ADR-0121).
        builder.Property(v => v.CpfCiphertext)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(v => v.Nome).HasMaxLength(250).IsRequired();
        builder.Property(v => v.Nivel).HasMaxLength(5).IsRequired();
        builder.Property(v => v.CursoNome).HasMaxLength(250).IsRequired();
        builder.Property(v => v.CursoCodigoEmec).HasMaxLength(20);
        builder.Property(v => v.CursoUnidadeNome).HasMaxLength(250).IsRequired();
        builder.Property(v => v.SituacaoDescricao).HasMaxLength(250).IsRequired();
        builder.Property(v => v.SituacaoVinculo).HasMaxLength(100);

        // Chave natural do módulo (id_discente do SIGAA) — a réplica localiza e faz
        // upsert por este identificador, nunca por CPF (ADR-0121: sem índice de
        // igualdade sobre o campo cifrado, pois o módulo não precisa buscar por CPF).
        builder.HasIndex(v => v.IdDiscenteSigaa)
            .IsUnique()
            .HasDatabaseName("ix_vinculo_discente_id_discente_sigaa");
    }
}
