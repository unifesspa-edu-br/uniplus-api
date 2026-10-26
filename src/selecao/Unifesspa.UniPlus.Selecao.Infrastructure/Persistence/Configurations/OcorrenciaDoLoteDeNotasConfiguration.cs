namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>Configuração EF Core das ocorrências de um lote: o que não virou nota. Nunca guarda CPF.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class OcorrenciaDoLoteDeNotasConfiguration : IEntityTypeConfiguration<OcorrenciaDoLoteDeNotas>
{
    public void Configure(EntityTypeBuilder<OcorrenciaDoLoteDeNotas> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // O IS NOT NULL explícito é necessário: cardinality(NULL) é NULL, e um CHECK que avalia NULL passa.
        builder.ToTable("ocorrencias_do_lote_de_notas", t =>
        {
            t.HasComment("O que não virou nota ao importar um lote: inscrito sem linha, linha de CPF sem inscrito e inscrito em conflito. Nunca guarda o CPF.");
            t.HasCheckConstraint(
                "ck_ocorrencias_do_lote_de_notas_forma",
                "(tipo = 1 AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NULL)"
                + " OR (tipo = 2 AND inscricao_id IS NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) = 1)"
                + " OR (tipo = 3 AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) >= 2)");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.LoteId).IsRequired();

        builder.Property(o => o.Tipo)
            .HasConversion<short>()
            .IsRequired()
            .HasComment("1 = inscrito sem linha, 2 = linha sem inscrito, 3 = conflito.");

        builder.Property(o => o.InscricaoId);

        builder.Property(o => o.NumerosDasLinhas)
            .HasColumnType("integer[]")
            .HasComment("Os números das linhas do arquivo, a partir de 1.");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(o => o.InscricaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.LoteId)
            .HasDatabaseName("ix_ocorrencias_do_lote_de_notas_lote");
    }
}
