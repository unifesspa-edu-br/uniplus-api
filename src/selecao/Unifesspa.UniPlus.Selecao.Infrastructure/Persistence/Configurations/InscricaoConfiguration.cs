namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class InscricaoConfiguration : IEntityTypeConfiguration<Inscricao>
{
    public void Configure(EntityTypeBuilder<Inscricao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("inscricoes");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Modalidade).HasConversion<int>().IsRequired();
        builder.Property(i => i.Status).HasConversion<int>().IsRequired();
        builder.Property(i => i.CodigoCursoPrimeiraOpcao).HasMaxLength(50);
        builder.Property(i => i.CodigoCursoSegundaOpcao).HasMaxLength(50);
        builder.Property(i => i.NumeroInscricao).HasMaxLength(50);

        builder.HasIndex(i => new { i.CandidatoId, i.EditalId });
        builder.HasIndex(i => i.NumeroInscricao).IsUnique();

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
