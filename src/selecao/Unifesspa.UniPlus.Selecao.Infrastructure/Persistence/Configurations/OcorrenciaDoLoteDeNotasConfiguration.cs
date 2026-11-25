namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
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

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        // O IS NOT NULL explícito é necessário: cardinality(NULL) é NULL, e um CHECK que avalia NULL passa.
        builder.ToTable("ocorrencias_da_importacao", t =>
        {
            t.HasComment("O que não virou nota ao importar um lote: inscrito sem linha, linha de CPF sem inscrito e inscrito em conflito. Nunca guarda o CPF.");
            t.HasCheckConstraint(
                "ck_ocorrencias_da_importacao_forma",
                $"(tipo = {SqlDosCodigosDeContrato.Literal(TipoDeOcorrenciaDoLote.InscritoSemLinha)} AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NULL)"
                + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDeOcorrenciaDoLote.LinhaSemInscrito)} AND inscricao_id IS NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) = 1)"
                + $" OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDeOcorrenciaDoLote.Conflito)} AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) >= 2)");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever().HasComment("Identificador da ocorrência na importação.");

        builder.Property(o => o.LoteId).HasColumnName("importacao_id").IsRequired().HasComment("Identificador da importação que registrou a ocorrência.");

        builder.Property(o => o.Tipo)
            .HasMaxLength(18)
            .IsRequired()
            .HasComment("Tipo de ocorrência no lote de notas.");

        builder.Property(o => o.InscricaoId).HasComment("Identificador da inscrição afetada, quando encontrada.");

        builder.Property(o => o.NumerosDasLinhas)
            .HasColumnType("integer[]")
            .HasComment("Os números das linhas do arquivo, a partir de 1.");

        builder.Property(o => o.CpfMascarado)
            .HasColumnName("cpf_parcialmente_oculto")
            .HasMaxLength(Cpf.TamanhoMascarado)
            .HasComment("O CPF mascarado da linha sem inscrito (***.999.999-**); nunca o CPF puro.");

        builder.Property(o => o.Nome)
            .HasMaxLength(OcorrenciaDoLoteDeNotas.TamanhoMaximoDoNome)
            .HasComment("O nome que o arquivo traz para a linha sem inscrito.");

        builder.Property(o => o.Sexo)
            .HasMaxLength(1)
            .HasComment("O sexo (F ou M) que o arquivo traz para a linha sem inscrito.");

        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(o => o.InscricaoId)
            .HasConstraintName("fk_ocorrencias_da_importacao_inscricao")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.LoteId)
            .HasDatabaseName("ix_ocorrencias_da_importacao_importacao");
    }

}
