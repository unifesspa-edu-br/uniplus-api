namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class ChamadaConfiguration : IEntityTypeConfiguration<Chamada>
{
    public void Configure(EntityTypeBuilder<Chamada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("chamadas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Numero).IsRequired();
        builder.Property(c => c.Status).HasConversion<int>().IsRequired();
        builder.Property(c => c.DataPublicacao).IsRequired();
        builder.Property(c => c.PrazoManifestacao).IsRequired();

        builder.HasMany(c => c.Convocacoes).WithOne().HasForeignKey(cv => cv.ChamadaId);

        builder.HasIndex(c => new { c.EditalId, c.Numero }).IsUnique();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
