namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada pelo EF Core via reflection.")]
internal sealed class TipoEtapaConfiguration : IEntityTypeConfiguration<TipoEtapa>
{
    public void Configure(EntityTypeBuilder<TipoEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Um tipo que não compõe nota nem elimina não configura etapa nenhuma — toda etapa
        // declara um caráter, e nenhum sobraria para escolher.
        builder.ToTable("tipos_etapa", t => t.HasCheckConstraint(
            "ck_tipos_etapa_carater_admitido",
            "admite_pontuacao OR admite_eliminacao"));
        builder.HasKey(tipo => tipo.Id);
        builder.Property(tipo => tipo.Id).ValueGeneratedNever();
        builder.Property(tipo => tipo.Codigo).HasMaxLength(64).IsRequired();
        builder.Property(tipo => tipo.Nome).HasMaxLength(200).IsRequired();
        builder.Property(tipo => tipo.Descricao).HasMaxLength(1000);
        builder.Property(tipo => tipo.Ativo).IsRequired();
        builder.Property(tipo => tipo.AdmitePontuacao).IsRequired();
        builder.Property(tipo => tipo.AdmiteEliminacao).IsRequired();
        builder.Property(tipo => tipo.CreatedBy).HasMaxLength(255);
        builder.Property(tipo => tipo.UpdatedBy).HasMaxLength(255);

        // A unicidade abrange também itens inativos: código de domínio nunca é reutilizado.
        builder.HasIndex(tipo => tipo.Codigo)
            .IsUnique()
            .HasDatabaseName("ix_tipos_etapa_codigo");
        builder.HasIndex(tipo => tipo.Ativo)
            .HasDatabaseName("ix_tipos_etapa_ativo");
    }
}
