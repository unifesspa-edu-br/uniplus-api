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

        // Índice composto na ordem exata em que a leitura dos documentos de um
        // processo pede as linhas: filtra por processo e já entrega o resultado
        // ordenado, sem passo de sort. As colunas de ordenação são descendentes
        // porque a lista vai do envio mais recente para o mais antigo, e um
        // índice ascendente serviria o filtro mas deixaria o sort de volta.
        builder.HasIndex(d => new { d.ProcessoSeletivoId, d.CreatedAt, d.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_documentos_edital_processo_recentes");
    }
}
