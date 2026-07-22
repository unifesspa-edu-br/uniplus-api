namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core de <see cref="FatoColetado"/> (Story #926) — entidade filha de
/// <c>ProcessoSeletivo</c>, <c>EntityBase</c> puro (sem soft-delete). Substituível por inteiro
/// junto com o processo, mesmo padrão de <c>DocumentoExigido</c>.
/// </summary>
public sealed class FatoColetadoConfiguration : IEntityTypeConfiguration<FatoColetado>
{
    private const int FatoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<FatoColetado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("fatos_coletados");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.FatoCodigo).HasMaxLength(FatoCodigoMaxLength).IsRequired();
        builder.Property(f => f.Ordem).IsRequired();

        // As duas unicidades são invariantes do agregado, feitas cumprir em
        // DefinirFatosColetados; os índices as garantem também contra escrita concorrente e
        // contra qualquer caminho que não passe pelo agregado.
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.FatoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_fatos_coletados_processo_fato");

        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_fatos_coletados_processo_ordem");

        builder.HasMany(f => f.Precondicoes)
            .WithOne()
            .HasForeignKey(c => c.FatoColetadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Precondicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
