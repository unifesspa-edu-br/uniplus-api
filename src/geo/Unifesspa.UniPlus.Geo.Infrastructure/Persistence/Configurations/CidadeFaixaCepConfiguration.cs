namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="CidadeFaixaCep"/> — faixas de CEP de um município. FK
/// intra-banco para <c>cidade</c>. A chave natural <c>(cidade_id, cep_inicial,
/// cep_final)</c> é UNIQUE — idempotência do upsert do ETL; com <c>cidade_id</c> à
/// esquerda também cobre a FK e o lookup por cidade. CHECK/sobreposição ficam no
/// ETL/lookup (carga tolerante, ADR-0092).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CidadeFaixaCepConfiguration : IEntityTypeConfiguration<CidadeFaixaCep>
{
    public void Configure(EntityTypeBuilder<CidadeFaixaCep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cidade_faixa_cep");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.CepInicial).IsRequired();
        builder.Property(f => f.CepFinal).IsRequired();

        builder.HasOne<Cidade>()
            .WithMany()
            .HasForeignKey(f => f.CidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chave natural única (cidade + faixa) — idempotência do ETL. cidade_id à
        // esquerda cobre o índice da FK e o lookup por cidade.
        builder.HasIndex(f => new { f.CidadeId, f.CepInicial, f.CepFinal })
            .IsUnique()
            .HasDatabaseName("ix_cidade_faixa_cep_natural");

        // Índice de range parcial para o caminho frio do lookup de CEP (#704): o
        // predicado global por CEP (cep_inicial <= @cep AND cep_final >= @cep) não casa
        // com o índice natural parent-first (cidade_id à esquerda). B-tree
        // (cep_inicial, cep_final) só sobre linhas vigentes — o lookup filtra vigente.
        builder.HasIndex(f => new { f.CepInicial, f.CepFinal })
            .HasFilter("vigente")
            .HasDatabaseName("ix_cidade_faixa_cep_range");

        builder.ConfigurarProveniencia(f => f.VersaoDataset, f => f.Vigente);
        builder.HasIndex(f => f.VersaoDataset)
            .HasDatabaseName("ix_cidade_faixa_cep_versao_dataset");
    }
}
