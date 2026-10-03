namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ModeloDeDocumentoConfiguration : IEntityTypeConfiguration<ModeloDeDocumento>
{
    public void Configure(EntityTypeBuilder<ModeloDeDocumento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("modelos_de_documento");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.NomeArquivo).HasMaxLength(ModeloDeDocumento.NomeArquivoMaxLength).IsRequired();
        builder.Property(m => m.Formato).HasConversion<int>().IsRequired();
        builder.Property(m => m.ObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(m => m.ObjectKeyConfirmado).HasMaxLength(500);
        builder.Property(m => m.Status).HasConversion<int>().IsRequired();
        builder.Property(m => m.HashSha256).HasMaxLength(64);
        builder.Ignore(m => m.Extensao);
        builder.Ignore(m => m.ContentType);

        // Vinculado ao processo por FK, sem navegação: não é filho do agregado.
        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(m => m.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ProcessoSeletivoId).HasDatabaseName("ix_modelos_de_documento_processo");
    }
}
