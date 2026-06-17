namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="Logradouro"/> (folha, ~1,4M linhas). O <c>cep</c> é
/// indexado mas NÃO único; a idempotência do upsert vem da chave composta UNIQUE
/// <c>(cep, nome_normalizado, cidade_id)</c>, cujo prefixo <c>cep</c> também serve o
/// lookup por CEP (F4) — por isso não há índice standalone de <c>cep</c> (seria
/// redundante numa tabela desse volume). FK intra-banco para <c>cidade</c>
/// (obrigatória) e <c>distrito</c>/<c>bairro</c> (opcionais); coordenada GIST e
/// trigram em <c>nome_normalizado</c> (ADR-0091).
/// </summary>
/// <remarks>
/// <para><strong>Coerência hierárquica:</strong> distrito/bairro são FKs simples e
/// independentes da cidade; garantir que pertençam à mesma cidade é
/// responsabilidade do ETL (dado autoritativo externo, ADR-0092) — não há FK
/// composta cruzando cidade (trade-off aceito para reference data público).</para>
/// <para><strong>Volume:</strong> os índices pesados (trigram/GIST) são definidos
/// aqui no schema; a carga em lote da F3 pode recriá-los após o COPY inicial para
/// acelerar a importação (estratégia do ETL, não do schema).</para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class LogradouroConfiguration : IEntityTypeConfiguration<Logradouro>
{
    public void Configure(EntityTypeBuilder<Logradouro> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("logradouro");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Cep).IsRequired();
        builder.Property(l => l.Nome).IsRequired();
        builder.Property(l => l.NomeNormalizado).IsRequired();
        builder.Property(l => l.Uf).IsRequired();
        builder.Property(l => l.Latitude).HasPrecision(9, 6);
        builder.Property(l => l.Longitude).HasPrecision(9, 6);
        builder.ConfigurarCoordenada(l => l.Coordenada);

        builder.HasOne<Cidade>()
            .WithMany()
            .HasForeignKey(l => l.CidadeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Distrito>()
            .WithMany()
            .HasForeignKey(l => l.DistritoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bairro>()
            .WithMany()
            .HasForeignKey(l => l.BairroId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Chave de upsert única; o prefixo cep cobre o lookup por CEP (F4).
        builder.HasIndex(l => new { l.Cep, l.NomeNormalizado, l.CidadeId })
            .IsUnique()
            .HasDatabaseName("ix_logradouro_natural");

        // Índices das FKs (a chave composta acima não tem cidade_id à esquerda).
        builder.HasIndex(l => l.CidadeId).HasDatabaseName("ix_logradouro_cidade_id");
        builder.HasIndex(l => l.DistritoId).HasDatabaseName("ix_logradouro_distrito_id");
        builder.HasIndex(l => l.BairroId).HasDatabaseName("ix_logradouro_bairro_id");

        builder.HasIndex(l => l.Coordenada)
            .HasMethod("gist")
            .HasDatabaseName("ix_logradouro_coordenada");

        builder.HasIndex(l => l.NomeNormalizado)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_logradouro_nome_trgm");

        builder.ConfigurarProveniencia(l => l.VersaoDataset, l => l.Vigente);
        builder.HasIndex(l => l.VersaoDataset)
            .HasDatabaseName("ix_logradouro_versao_dataset");
    }
}
