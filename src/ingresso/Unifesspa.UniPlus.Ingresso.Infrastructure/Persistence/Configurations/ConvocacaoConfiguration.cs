namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ConvocacaoConfiguration : IEntityTypeConfiguration<Convocacao>
{
    public void Configure(EntityTypeBuilder<Convocacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("convocacoes");
        builder.HasKey(c => c.Id);

        builder.OwnsOne(c => c.Protocolo, pb =>
        {
            pb.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            pb.Property(p => p.Valor).HasColumnName("protocolo").HasMaxLength(50).IsRequired();
            pb.HasIndex(p => p.Valor).IsUnique();
        });

        builder.Property(c => c.Status).HasConversion<int>().IsRequired();
        builder.Property(c => c.Posicao).IsRequired();
        builder.Property(c => c.CodigoCurso).HasMaxLength(50).IsRequired();
    }
}
