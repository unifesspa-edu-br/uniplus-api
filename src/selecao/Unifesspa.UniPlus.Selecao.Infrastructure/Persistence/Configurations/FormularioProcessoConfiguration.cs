namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="FormularioProcesso"/> (UNI-REQ-0144): filho de
/// <c>ProcessoSeletivo</c>, no máximo um por finalidade, com as etapas em cascata.
/// </summary>
public sealed class FormularioProcessoConfiguration : IEntityTypeConfiguration<FormularioProcesso>
{
    public void Configure(EntityTypeBuilder<FormularioProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("formularios_processo");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Finalidade).HasConversion<int>().IsRequired();
        builder.Property(f => f.Titulo).HasMaxLength(FormaDoCabecalho.TituloMaxLength);
        builder.Property(f => f.ModeloOrigemCodigo).HasMaxLength(FormularioProcesso.ModeloOrigemCodigoMaxLength);
        builder.Ignore(f => f.Estrutura);

        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Finalidade })
            .IsUnique()
            .HasDatabaseName("ux_formularios_processo_processo_finalidade");

        builder.HasMany(f => f.Etapas)
            .WithOne()
            .HasForeignKey(e => e.FormularioProcessoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Etapas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Configuração EF Core de <see cref="EtapaFormulario"/>: código e ordem únicos no formulário.</summary>
public sealed class EtapaFormularioConfiguration : IEntityTypeConfiguration<EtapaFormulario>
{
    public void Configure(EntityTypeBuilder<EtapaFormulario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("etapas_formulario");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Codigo).HasMaxLength(FormaDaEtapa.CodigoMaxLength).IsRequired();
        builder.Property(e => e.Ordem).IsRequired();
        builder.Property(e => e.Tipo).HasConversion<int>().IsRequired();
        builder.Property(e => e.Bloco).HasConversion<int>().IsRequired();
        builder.Property(e => e.Titulo).HasMaxLength(FormaDaEtapa.TituloMaxLength).IsRequired();
        builder.Property(e => e.Descricao).HasMaxLength(FormaDaEtapa.TextoMaxLength);
        builder.Property(e => e.Aviso).HasMaxLength(FormaDaEtapa.TextoMaxLength);
        builder.Property(e => e.Exibicao)
            .HasConversion(ConversoresDeRegras.Predicado, ConversoresDeRegras.ComparadorDePredicado)
            .HasColumnType("jsonb");
        builder.Ignore(e => e.Estrutura);
        builder.Ignore(e => e.FatosCitados);
        builder.Ignore(e => e.Condicoes);

        builder.HasIndex(e => new { e.FormularioProcessoId, e.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_etapas_formulario_formulario_codigo");
        builder.HasIndex(e => new { e.FormularioProcessoId, e.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_etapas_formulario_formulario_ordem");
    }
}
