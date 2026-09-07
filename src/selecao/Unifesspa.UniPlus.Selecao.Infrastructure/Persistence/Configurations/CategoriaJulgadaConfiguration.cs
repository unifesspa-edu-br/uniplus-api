namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core de <see cref="CategoriaJulgada"/> — filha 0..* de
/// <see cref="BancaRequerida"/>. Os limites de comprimento espelham
/// <c>LimitesDoEnvelope</c> (constantes duplicadas por convenção do repo, verificadas
/// pelo fitness <c>LimitesDoEnvelopeBatemComOSchemaTests</c>).
/// </summary>
public sealed class CategoriaJulgadaConfiguration : IEntityTypeConfiguration<CategoriaJulgada>
{
    private const int CodigoMaxLength = 50;

    public void Configure(EntityTypeBuilder<CategoriaJulgada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("categorias_julgadas");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CategoriaDocumentoOrigemId).IsRequired();
        builder.Property(c => c.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();

        // A mesma categoria declarada uma única vez por banca. A rejeição no domínio é
        // check-then-act não-atômico, e esta constraint é a defesa atômica — mesmo padrão
        // de ux_produtos_da_fase_ato. A banca é sempre reposta por instância nova ao
        // regravar o cronograma, então nenhuma reposição reocupa o mesmo par.
        builder.HasIndex(c => new { c.BancaRequeridaId, c.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_categorias_julgadas_codigo");
    }
}
