namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

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

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("bases_legais_do_bonus_regional");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TipoInstrumento)
            .HasConversion(TipoInstrumentoConverter)
            .HasMaxLength(30)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("ck_bases_legais_do_bonus_regional_tipo_instrumento", "tipo_instrumento IN ('LEI','DECRETO','PORTARIA','RESOLUCAO','INSTRUCAO_NORMATIVA','PARECER','EDITAL')"));

        builder.Property(e => e.Identificacao).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Descricao).HasMaxLength(2000).IsRequired();

        builder.Property(e => e.CreatedBy).HasMaxLength(255);
        builder.Property(e => e.UpdatedBy).HasMaxLength(255);

        builder.OwnsMany(e => e.Municipios, m =>
        {
            m.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            m.ToTable("municipios_da_base_legal_bonus");
            m.WithOwner().HasForeignKey("BaseLegalBonusRegionalId").HasConstraintName("fk_municipios_da_base_legal_bonus_base_legal_do_bonus_regional");

            m.Property(x => x.CodigoIbge).HasColumnName("codigo_ibge").HasMaxLength(7).IsRequired();
            m.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(150).IsRequired();
            m.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2).IsRequired();

            m.ToTable(t => t.HasCheckConstraint("ck_municipios_da_base_legal_bonus_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'"));
        });

        builder.HasIndex(e => e.TipoInstrumento).HasDatabaseName("ix_bases_legais_do_bonus_regional_tipo_instrumento");
    }
}
