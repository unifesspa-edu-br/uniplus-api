namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class UnidadeIdentificadorHistoricoConfiguration
    : IEntityTypeConfiguration<UnidadeIdentificadorHistorico>
{
    public void Configure(EntityTypeBuilder<UnidadeIdentificadorHistorico> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("unidade_identificador_historico");
        builder.HasKey(h => h.Id);

        // EntityBase gera UUIDv7 no cliente. Sem explicitar isso, o EF trata
        // novas entradas adicionadas a uma Unidade já rastreada como existentes
        // e emite UPDATE em vez de INSERT ao fechar/abrir histórico.
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.UnidadeId).IsRequired();
        builder.Property(h => h.TipoIdentificador).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.Valor).HasMaxLength(100).IsRequired();
        builder.Property(h => h.VigenciaInicio).IsRequired();
        builder.Property(h => h.VigenciaFim);
        builder.Property(h => h.MotivoMudanca).HasMaxLength(500);

        // Índice para consultas históricas por unidade + tipo + período
        builder.HasIndex(h => new { h.UnidadeId, h.TipoIdentificador, h.VigenciaInicio })
            .HasDatabaseName("ix_uid_hist_unidade_tipo_inicio");

        // Histórico é append-only — sem query filter de soft-delete
    }
}
