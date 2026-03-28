namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;

public sealed class DocumentoMatriculaConfiguration : IEntityTypeConfiguration<DocumentoMatricula>
{
    public void Configure(EntityTypeBuilder<DocumentoMatricula> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("documentos_matricula");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TipoDocumento).HasMaxLength(100).IsRequired();
        builder.Property(d => d.NomeArquivo).HasMaxLength(500).IsRequired();
        builder.Property(d => d.CaminhoStorage).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.MotivoRejeicao).HasMaxLength(1000);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
