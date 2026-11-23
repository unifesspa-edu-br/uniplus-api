namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class BaseLegalBonusRegionalConfiguration : IEntityTypeConfiguration<BaseLegalBonusRegional>
{
    public void Configure(EntityTypeBuilder<BaseLegalBonusRegional> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("bases_legais_do_fada", t => t.HasComment("Bases legais do Fator de Atenuação das Desigualdades de Acesso (FADA)."));

        builder.HasKey(e => e.Id).HasName("pk_bases_legais_do_fada");
        builder.Property(e => e.Id).HasComment("Identificador da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA).");

        builder.Property(e => e.Codigo)
            .HasComment("Código da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA).")
            .HasConversion<CodigoBaseLegalBonusRegionalValueConverter>()
            .HasMaxLength(CodigoBaseLegalBonusRegional.TamanhoMaximo)
            .IsRequired();

        builder.Property(e => e.TipoInstrumento)
            .HasComment("Tipo do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA): LEI, DECRETO, PORTARIA, RESOLUCAO, INSTRUCAO_NORMATIVA, PARECER ou EDITAL.")
            .HasMaxLength(30)
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("ck_bases_legais_do_fada_tipo_instrumento", $"tipo_instrumento IN ({CodigosDoTipoDeInstrumentoEmSql()})"));

        builder.Property(e => e.Identificacao).HasComment("Identificação do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA).").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Descricao).HasComment("Descrição da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA).").HasMaxLength(2000).IsRequired();

        builder.Property(e => e.CreatedBy).HasMaxLength(255);
        builder.Property(e => e.UpdatedBy).HasMaxLength(255);

        builder.OwnsMany(e => e.Municipios, m =>
        {
            m.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            m.ToTable("municipios_da_base_legal_fada", t => t.HasComment("Municípios abrangidos pela base legal do Fator de Atenuação das Desigualdades de Acesso (FADA)."));
            m.HasKey("BaseLegalBonusRegionalId", "Id").HasName("pk_municipios_da_base_legal_fada");
            m.Property<Guid>("BaseLegalBonusRegionalId").HasColumnName("base_legal_id").HasComment("Identificador da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA).");
            m.Property<int>("Id").HasComment("Identificador do município na base legal do Fator de Atenuação das Desigualdades de Acesso (FADA).");
            m.WithOwner().HasForeignKey("BaseLegalBonusRegionalId").HasConstraintName("fk_municipios_da_base_legal_fada_base_legal_id");

            m.Property(x => x.CodigoIbge).HasComment("Código IBGE do município abrangido pelo Fator de Atenuação das Desigualdades de Acesso (FADA).").HasColumnName("codigo_ibge").HasMaxLength(7).IsRequired();
            m.Property(x => x.Nome).HasComment("Nome do município abrangido pelo Fator de Atenuação das Desigualdades de Acesso (FADA).").HasColumnName("nome").HasMaxLength(150).IsRequired();
            m.Property(x => x.Uf).HasComment("Sigla da unidade federativa do município abrangido pelo Fator de Atenuação das Desigualdades de Acesso (FADA).").HasColumnName("uf").HasMaxLength(2).IsRequired();

            m.ToTable(t => t.HasCheckConstraint("ck_municipios_da_base_legal_fada_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'"));
        });

        builder.HasIndex(e => e.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.BaseLegalBonusRegionalCodigoVivo);

        builder.HasIndex(e => e.TipoInstrumento).HasDatabaseName("ix_bases_legais_do_fada_tipo_instrumento");
    }

    private static string CodigosDoTipoDeInstrumentoEmSql() =>
        string.Join(",", CatalogoDe<TipoInstrumentoNormativo>.Todos().Select(static d => $"'{d.Codigo}'"));
}
