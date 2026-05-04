namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class CotaConfiguration : IEntityTypeConfiguration<Cota>
{
    public void Configure(EntityTypeBuilder<Cota> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cotas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Modalidade).HasConversion<int>().IsRequired();
        builder.Property(c => c.PercentualVagas).HasPrecision(5, 2).IsRequired();
        builder.Property(c => c.Descricao).HasMaxLength(500);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
