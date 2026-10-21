namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ApresentacaoDeDocumentoConfiguration : IEntityTypeConfiguration<ApresentacaoDeDocumento>
{
    public void Configure(EntityTypeBuilder<ApresentacaoDeDocumento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("apresentacoes_de_documento", t => t.HasComment(
            "A apresentação de um documento exigido pela inscrição: a unidade que a exigência conta, com um ou mais arquivos."));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.ExigenciaId)
            .HasComment("O DocumentoExigido.Id da árvore congelada da versão da inscrição. Sem chave estrangeira para a configuração viva (ADR-0070).");
        builder.Property(a => a.OcorrenciaId)
            .HasMaxLength(200)
            .HasComment("A ocorrência do grupo repetível a que o documento se refere; nulo quando a exigência não se repete.");

        // Os arquivos saem junto da inscrição: o descarte do rascunho não deixa linha órfã.
        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(a => a.InscricaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.InscricaoId, a.ExigenciaId })
            .HasDatabaseName("ix_apresentacoes_de_documento_inscricao_exigencia");
    }
}
