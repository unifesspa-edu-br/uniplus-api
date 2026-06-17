namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="EstadoFaixaCep"/> — faixas de CEP de uma UF (várias por
/// estado). FK intra-banco para <c>estado</c>. A chave natural <c>(estado_id,
/// cep_inicial, cep_final)</c> é UNIQUE — garante a idempotência do upsert do ETL
/// (não duplica a mesma faixa para a mesma UF) e, com <c>estado_id</c> à esquerda,
/// também serve o lookup por estado e a integridade da FK. A consistência
/// <c>cep_inicial ≤ cep_final</c> e a não-sobreposição são validadas no ETL/lookup
/// (F3/F4) — a carga em lote é tolerante (ADR-0092), sem CHECK que a interrompa.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class EstadoFaixaCepConfiguration : IEntityTypeConfiguration<EstadoFaixaCep>
{
    public void Configure(EntityTypeBuilder<EstadoFaixaCep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("estado_faixa_cep");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.CepInicial).IsRequired();
        builder.Property(f => f.CepFinal).IsRequired();

        builder.HasOne<Estado>()
            .WithMany()
            .HasForeignKey(f => f.EstadoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chave natural única (estado + faixa) — idempotência do ETL. estado_id à
        // esquerda cobre o índice da FK e o lookup por estado.
        builder.HasIndex(f => new { f.EstadoId, f.CepInicial, f.CepFinal })
            .IsUnique()
            .HasDatabaseName("ix_estado_faixa_cep_natural");

        builder.ConfigurarProveniencia(f => f.VersaoDataset, f => f.Vigente);
        builder.HasIndex(f => f.VersaoDataset)
            .HasDatabaseName("ix_estado_faixa_cep_versao_dataset");
    }
}
