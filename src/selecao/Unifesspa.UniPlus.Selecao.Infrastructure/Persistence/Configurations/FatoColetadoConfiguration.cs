namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="FatoColetado"/> (Story #926; apresentação —
/// Rotulo/TipoRenderizacao/Obrigatorio — Story #559) — entidade filha de
/// <c>ProcessoSeletivo</c>, <c>EntityBase</c> puro (sem soft-delete). Substituível por inteiro
/// junto com o processo, mesmo padrão de <c>DocumentoExigido</c>.
/// </summary>
public sealed class FatoColetadoConfiguration : IEntityTypeConfiguration<FatoColetado>
{
    public void Configure(EntityTypeBuilder<FatoColetado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("fatos_coletados");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.FatoCodigo).HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(f => f.Ordem).IsRequired();
        builder.Property(f => f.Rotulo).HasMaxLength(FormaDoItem.RotuloMaxLength).IsRequired();
        builder.Property(f => f.TipoRenderizacao).HasConversion<int>().IsRequired();
        builder.Property(f => f.Obrigatoriedade)
            .HasConversion(ConversoresDeRegras.Obrigatoriedade, ConversoresDeRegras.ComparadorDeObrigatoriedade)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(f => f.Ajuda).HasMaxLength(FormaDoItem.AjudaMaxLength);
        builder.Property(f => f.PedirConfirmacao).IsRequired();
        builder.Property(f => f.Restricoes)
            .HasConversion(ConversoresDeRegras.Restricoes, ConversoresDeRegras.ComparadorDeRestricoes)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(f => f.OrigemValores).HasConversion<int>().IsRequired();
        builder.Property(f => f.Formato).HasMaxLength(FormaDoItem.FormatoMaxLength);
        builder.Ignore(f => f.OpcoesDoProcesso);
        builder.Ignore(f => f.Condicoes);
        builder.Ignore(f => f.Exibicao);
        builder.Property(f => f.Finalidade).HasConversion<int>().IsRequired();
        builder.Property(f => f.EtapaCodigo).HasMaxLength(FormaDaEtapa.CodigoMaxLength);

        // As duas unicidades são invariantes do agregado, feitas cumprir em
        // DefinirFatosColetados; os índices as garantem também contra escrita concorrente e
        // contra qualquer caminho que não passe pelo agregado.
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.FatoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_fatos_coletados_processo_fato");

        // O fato tem um só produtor no processo, item ou campo de grupo. A ordem do item é única no
        // formulário, e a do campo de grupo, dentro do grupo; a ordem que o grupo divide com os
        // itens é invariante do agregado.
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Finalidade, f.Ordem })
            .IsUnique()
            .HasFilter("grupo_coletado_id IS NULL")
            .HasDatabaseName("ux_fatos_coletados_processo_finalidade_ordem");
        builder.HasIndex(f => new { f.GrupoColetadoId, f.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_fatos_coletados_grupo_ordem");

        builder.HasMany(f => f.Precondicoes)
            .WithOne()
            .HasForeignKey(c => c.FatoColetadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Precondicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
