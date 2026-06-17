namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class LocalOfertaConfiguration : IEntityTypeConfiguration<LocalOferta>
{
    public void Configure(EntityTypeBuilder<LocalOferta> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("local_oferta");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Referência de cidade do Geo (ADR-0090): código + display cache, sem FK
        // cross-banco para uniplus_geo.
        builder.Property(l => l.CidadeCodigoIbge)
            .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(l => l.CidadeNome).HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength).IsRequired();
        builder.Property(l => l.CidadeUf)
            .HasMaxLength(ReferenciaCidadeGeo.UfLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(l => l.CidadeOrigem).HasMaxLength(ReferenciaCidadeGeo.OrigemMaxLength);
        builder.Property(l => l.CidadeDisplayAtualizadoEm);

        builder.Property(l => l.Endereco).HasMaxLength(500);
        builder.Property(l => l.CodigoEmec).HasMaxLength(20);

        // Auditoria (IAuditableEntity)
        builder.Property(l => l.CreatedBy).HasMaxLength(255);
        builder.Property(l => l.UpdatedBy).HasMaxLength(255);

        // Campus responsável: FK intra-banco opcional (ADR-0065). Restrict no banco
        // — a remoção lógica do Campus é barrada pelo handler
        // (CampusErrorCodes.RemocaoBloqueadaPorLocalOferta); o RESTRICT cobre o
        // DELETE físico residual.
        builder.HasOne<Campus>()
            .WithMany()
            .HasForeignKey(l => l.CampusResponsavelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.CampusResponsavelId)
            .HasDatabaseName("ix_local_oferta_campus_responsavel_id");

        // Índice de relatório/filtro pela cidade (ADR-0090).
        builder.HasIndex(l => l.CidadeCodigoIbge)
            .HasDatabaseName("ix_local_oferta_cidade_codigo_ibge");
    }
}
