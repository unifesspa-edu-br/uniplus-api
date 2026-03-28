namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class EditalConfiguration : IEntityTypeConfiguration<Edital>
{
    public void Configure(EntityTypeBuilder<Edital> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("editais");
        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.NumeroEdital, nb =>
        {
            nb.Property(n => n.Numero).HasColumnName("numero_edital").IsRequired();
            nb.Property(n => n.Ano).HasColumnName("ano_edital").IsRequired();
        });

        builder.Property(e => e.Titulo).HasMaxLength(500).IsRequired();
        builder.Property(e => e.TipoProcesso).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.MaximoOpcoesCurso).HasDefaultValue(1);

        builder.OwnsOne(e => e.PeriodoInscricao, pb =>
        {
            pb.Property(p => p.Inicio).HasColumnName("periodo_inscricao_inicio");
            pb.Property(p => p.Fim).HasColumnName("periodo_inscricao_fim");
        });

        builder.OwnsOne(e => e.FormulaCalculo, fb =>
        {
            fb.Property(f => f.FatorDivisao).HasColumnName("fator_divisao").HasPrecision(18, 4);
            fb.Property(f => f.BonusRegionalPercentual).HasColumnName("bonus_regional_percentual").HasPrecision(5, 2);
        });

        builder.HasMany(e => e.Etapas).WithOne().HasForeignKey(et => et.EditalId);
        builder.HasMany(e => e.Cotas).WithOne().HasForeignKey(c => c.EditalId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
