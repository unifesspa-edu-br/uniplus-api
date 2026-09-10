namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class BaseLegalBonusRegionalConfiguration : IEntityTypeConfiguration<BaseLegalBonusRegional>
{
    // Mesmo mapeamento canônico usado por AbrangenciaConverter/StatusConverter
    // (DocumentoExigidoBaseLegalConfiguration) — nunca enum.ToString() cru: a coluna
    // carrega o mesmo token UPPER_SNAKE que o CHECK constraint e o wire aceitam, não o
    // nome do membro C# (que o CHECK sempre rejeitaria).
    private static readonly ValueConverter<TipoInstrumentoNormativo, string> TipoInstrumentoConverter =
        new(tipo => tipo.ToCodigo(), codigo => TipoInstrumentoNormativoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<BaseLegalBonusRegional> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("base_legal_bonus_regional");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TipoInstrumento)
            .HasConversion(TipoInstrumentoConverter)
            .HasMaxLength(30)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("CK_base_legal_bonus_regional_tipo_instrumento", "tipo_instrumento IN ('LEI','DECRETO','PORTARIA','RESOLUCAO','INSTRUCAO_NORMATIVA','PARECER')"));

        builder.Property(e => e.Identificacao).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Descricao).HasMaxLength(2000).IsRequired();

        builder.Property(e => e.CreatedBy).HasMaxLength(255);
        builder.Property(e => e.UpdatedBy).HasMaxLength(255);

        builder.OwnsMany(e => e.Municipios, m =>
        {
            m.ToTable("base_legal_bonus_regional_municipio");
            m.WithOwner().HasForeignKey("BaseLegalBonusRegionalId");

            m.Property(x => x.CodigoIbge).HasColumnName("codigo_ibge").HasMaxLength(7).IsRequired();
            m.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(150).IsRequired();
            m.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2).IsRequired();

            m.ToTable(t => t.HasCheckConstraint("CK_base_legal_bonus_regional_municipio_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'"));
        });

        builder.HasIndex(e => e.TipoInstrumento).HasDatabaseName("IX_base_legal_bonus_regional_tipo_instrumento");
    }
}
