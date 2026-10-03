namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="GrupoColetado"/> (UNI-REQ-0146), filha de
/// <c>ProcessoSeletivo</c> substituída por inteiro com ele, dona dos seus campos. A exibição e a
/// obrigatoriedade são lidas e gravadas sempre inteiras, junto do grupo, por isso ficam em <c>jsonb</c>.
/// </summary>
public sealed class GrupoColetadoConfiguration : IEntityTypeConfiguration<GrupoColetado>
{
    public void Configure(EntityTypeBuilder<GrupoColetado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("grupos_coletados");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Finalidade).HasConversion<int>().IsRequired();
        builder.Property(g => g.Codigo).HasMaxLength(FormaDoGrupo.CodigoMaxLength).IsRequired();
        builder.Property(g => g.Ordem).IsRequired();
        builder.Property(g => g.EtapaCodigo).HasMaxLength(FormaDaEtapa.CodigoMaxLength);
        builder.Property(g => g.Rotulo).HasMaxLength(FormaDoItem.RotuloMaxLength).IsRequired();
        builder.Property(g => g.Minimo).IsRequired();
        builder.Property(g => g.Maximo);
        builder.Property(g => g.Exibicao)
            .HasConversion(ConversoresDeRegras.Predicado, ConversoresDeRegras.ComparadorDePredicado)
            .HasColumnType("jsonb");
        builder.Property(g => g.Obrigatoriedade)
            .HasConversion(ConversoresDeRegras.Obrigatoriedade, ConversoresDeRegras.ComparadorDeObrigatoriedade)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Ignore(g => g.FatosCitados);
        builder.Ignore(g => g.Condicoes);

        // O código do grupo é único no processo, como o do fato; a ordem que ele compartilha com os
        // itens do formulário é invariante do agregado, conferida em DefinirFatosColetados.
        builder.HasIndex(g => new { g.ProcessoSeletivoId, g.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_grupos_coletados_processo_codigo");

        builder.HasMany(g => g.Subitens)
            .WithOne()
            .HasForeignKey(f => f.GrupoColetadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.Subitens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
