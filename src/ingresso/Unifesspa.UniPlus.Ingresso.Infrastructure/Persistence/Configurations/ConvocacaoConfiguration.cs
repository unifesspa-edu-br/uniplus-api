namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;

public sealed class ConvocacaoConfiguration : IEntityTypeConfiguration<Convocacao>
{
    public void Configure(EntityTypeBuilder<Convocacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("convocacoes");
        builder.HasKey(c => c.Id);

        builder.OwnsOne(c => c.Protocolo, pb =>
        {
            pb.Property(p => p.Valor).HasColumnName("protocolo").HasMaxLength(50).IsRequired();
            pb.HasIndex(p => p.Valor).IsUnique();
        });

        builder.Property(c => c.Status).HasConversion<int>().IsRequired();
        builder.Property(c => c.Posicao).IsRequired();
        builder.Property(c => c.CodigoCurso).HasMaxLength(50).IsRequired();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
