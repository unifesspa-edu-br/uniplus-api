namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
    }
}
