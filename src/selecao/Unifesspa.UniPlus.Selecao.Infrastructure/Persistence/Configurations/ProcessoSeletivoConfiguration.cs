namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

public sealed class ProcessoSeletivoConfiguration : IEntityTypeConfiguration<ProcessoSeletivo>
{
    public void Configure(EntityTypeBuilder<ProcessoSeletivo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("processos_seletivos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CodigoCurso).HasMaxLength(50).IsRequired();
        builder.Property(p => p.NomeCurso).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Campus).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Turno).HasMaxLength(50);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
