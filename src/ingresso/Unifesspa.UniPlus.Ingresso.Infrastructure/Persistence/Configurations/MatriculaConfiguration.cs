namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class MatriculaConfiguration : IEntityTypeConfiguration<Matricula>
{
    public void Configure(EntityTypeBuilder<Matricula> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("matriculas");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status).HasConversion<int>().IsRequired();
        builder.Property(m => m.CodigoCurso).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Observacoes).HasMaxLength(2000);

        builder.HasMany(m => m.Documentos).WithOne().HasForeignKey(d => d.MatriculaId);
    }
}
