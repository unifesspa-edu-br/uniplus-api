namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="FaseCronograma"/> (Story #851) — entidade filha
/// do agregado <see cref="ProcessoSeletivo"/>. Os limites de comprimento espelham
/// <c>LimitesDoEnvelope</c> (constantes duplicadas por convenção do repo, verificadas
/// pelo fitness <c>LimitesDoEnvelopeBatemComOSchemaTests</c>).
/// </summary>
public sealed class FaseCronogramaConfiguration : IEntityTypeConfiguration<FaseCronograma>
{
    private const int CodigoMaxLength = 60;
    private const int DonoInstitucionalMaxLength = 60;
    private const int TipoAtoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<FaseCronograma> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("fases_cronograma");
        builder.HasKey(f => f.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Ordem).IsRequired();
        builder.Property(f => f.FaseCanonicaOrigemId).IsRequired();
        builder.Property(f => f.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(f => f.DonoInstitucional).HasMaxLength(DonoInstitucionalMaxLength).IsRequired();
        builder.Property(f => f.OrigemData).HasConversion<int>().IsRequired();
        builder.Property(f => f.AtoProduzidoCodigo).HasMaxLength(TipoAtoCodigoMaxLength);

        // Ordem e fase canônica únicas por processo — a rejeição em
        // ProcessoSeletivo.DefinirCronogramaFases é check-then-act não-atômico; a
        // constraint do banco é a defesa realmente atômica (mesmo padrão de
        // CriterioDesempateConfiguration).
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_fases_cronograma_processo_ordem");
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.FaseCanonicaOrigemId })
            .IsUnique()
            .HasDatabaseName("ux_fases_cronograma_processo_fase_canonica");

        builder.HasMany(f => f.BancasRequeridas)
            .WithOne()
            .HasForeignKey(b => b.FaseCronogramaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.RegraRecurso)
            .WithOne()
            .HasForeignKey<RegraRecursoFase>(r => r.FaseCronogramaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.BancasRequeridas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
