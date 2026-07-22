namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class DocumentoEditalConfiguration : IEntityTypeConfiguration<DocumentoEdital>
{
    public void Configure(EntityTypeBuilder<DocumentoEdital> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("documentos_edital");
        builder.HasKey(d => d.Id);
        // Guid v7 gerado no domínio (EntityBase) — mesma convenção de ProcessoSeletivoConfiguration.
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.ObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(d => d.ObjectKeyConfirmado).HasMaxLength(500);
        builder.Property(d => d.Status).HasConversion<int>().IsRequired();
        builder.Property(d => d.HashSha256).HasMaxLength(64);

        // Vínculo por FK ao processo (não é entidade filha do agregado — sem
        // navegação inversa em ProcessoSeletivo, ver comentário da entidade).
        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(d => d.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.ProcessoSeletivoId);
    }
}
