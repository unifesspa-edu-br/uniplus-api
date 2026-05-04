namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class EtapaConfiguration : IEntityTypeConfiguration<Etapa>
{
    public void Configure(EntityTypeBuilder<Etapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("etapas");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Nome).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Tipo).HasConversion<int>().IsRequired();
        builder.Property(e => e.Peso).HasPrecision(5, 2).IsRequired();
        builder.Property(e => e.Ordem).IsRequired();
        builder.Property(e => e.NotaMinima).HasPrecision(5, 2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
