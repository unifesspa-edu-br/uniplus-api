namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RespostaDaInscricaoRecordConfiguration : IEntityTypeConfiguration<RespostaDaInscricaoRecord>
{
    public void Configure(EntityTypeBuilder<RespostaDaInscricaoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("respostas_da_inscricao", t =>
        {
            t.HasComment("Um valor respondido na inscrição, na coluna do tipo do fato, com a origem: informado pelo candidato ou derivado de outra resposta.");
            t.HasCheckConstraint(
                "ck_respostas_da_inscricao_valor_do_tipo",
                "num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo BETWEEN 1 AND 5 AND ("
                + "valor_cifrado IS NOT NULL"
                + " OR (tipo = 1 AND valor_booleano IS NOT NULL)"
                + " OR (tipo = 2 AND valor_numero IS NOT NULL)"
                + " OR (tipo = 3 AND valor_codigo IS NOT NULL)"
                + " OR (tipo = 4 AND valor_texto IS NOT NULL)"
                + " OR (tipo = 5 AND valor_data IS NOT NULL))");
            t.HasCheckConstraint("ck_respostas_da_inscricao_origem", "origem IN (1, 2)");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.FatoCodigo).HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(r => r.Origem).HasConversion<int>().IsRequired();
        builder.Property(r => r.Tipo).HasConversion<int>().IsRequired();
        builder.Property(r => r.ValorCodigo).HasMaxLength(OpcaoDeclaradaFato.CodigoMaxLength);
        builder.Property(r => r.ValorCifrado)
            .HasColumnType("bytea")
            .HasComment("Resposta cifrada (AES-GCM, ADR-0121) — nunca texto claro: o número de documento, com a chave dos identificadores, ou o dado sensível de cifra individual, com a chave dos sensíveis.");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(r => r.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OcorrenciaDeGrupoDaInscricaoRecord>()
            .WithMany()
            .HasForeignKey(r => r.OcorrenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.InscricaoId, r.OcorrenciaId, r.FatoCodigo, r.Posicao })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_respostas_da_inscricao_valor");

        // A classificação e a habilitação filtram candidatos por fato e valor; texto livre não é filtro.
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorCodigo })
            .HasFilter("valor_codigo IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_fato_codigo");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorBooleano })
            .HasFilter("valor_booleano IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_fato_booleano");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorNumero })
            .HasFilter("valor_numero IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_fato_numero");
        builder.HasIndex(r => new { r.FatoCodigo, r.ValorData })
            .HasFilter("valor_data IS NOT NULL")
            .HasDatabaseName("ix_respostas_da_inscricao_fato_data");
    }
}
