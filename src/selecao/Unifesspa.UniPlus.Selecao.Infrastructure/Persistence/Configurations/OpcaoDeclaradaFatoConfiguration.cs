namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core de <see cref="OpcaoDeclaradaFato"/> (issue #1619) — entidade filha de
/// <c>ProcessoSeletivo</c>, <c>EntityBase</c> puro, substituída por fato pelo agregado.
/// </summary>
public sealed class OpcaoDeclaradaFatoConfiguration : IEntityTypeConfiguration<OpcaoDeclaradaFato>
{
    public void Configure(EntityTypeBuilder<OpcaoDeclaradaFato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("opcoes_declaradas_fato");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.FatoCodigo).HasMaxLength(OpcaoDeclaradaFato.FatoCodigoMaxLength).IsRequired();
        builder.Property(o => o.Codigo).HasMaxLength(OpcaoDeclaradaFato.CodigoMaxLength).IsRequired();
        builder.Property(o => o.Rotulo).HasMaxLength(OpcaoDeclaradaFato.RotuloMaxLength).IsRequired();
        builder.Property(o => o.Ordem).IsRequired();

        // Código único por fato no processo: invariante do agregado, também contra escrita
        // concorrente e caminho que não passe por ele.
        builder.HasIndex(o => new { o.ProcessoSeletivoId, o.FatoCodigo, o.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_opcoes_declaradas_fato_processo_fato_codigo");
    }
}
