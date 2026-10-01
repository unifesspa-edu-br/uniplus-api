namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Regras.Formularios;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada pelo EF Core via reflection.")]
internal sealed class ModeloFormularioConfiguration : IEntityTypeConfiguration<ModeloFormulario>
{
    private static readonly ValueConverter<FinalidadeFormulario, string> FinalidadeConverter =
        new(finalidade => EstruturaFormulario.ParaToken(finalidade), token => EstruturaFormulario.FinalidadeDoToken(token));

    public void Configure(EntityTypeBuilder<ModeloFormulario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Domínio fechado da finalidade (defesa em profundidade contra inserts crus).
        builder.ToTable("modelos_formulario", static tabela => tabela.HasCheckConstraint(
            "ck_modelos_formulario_finalidade",
            $"finalidade IN ('{EstruturaFormulario.FinalidadeInscricao}', '{EstruturaFormulario.FinalidadeIsencaoTaxa}', '{EstruturaFormulario.FinalidadeHabilitacao}')"));
        builder.HasKey(modelo => modelo.Id);
        builder.Property(modelo => modelo.Id).ValueGeneratedNever();
        builder.Property(modelo => modelo.Codigo).HasMaxLength(ModeloFormulario.CodigoMaxLength).IsRequired();
        builder.Property(modelo => modelo.Nome).HasMaxLength(ModeloFormulario.NomeMaxLength).IsRequired();
        builder.Property(modelo => modelo.Descricao).HasMaxLength(ModeloFormulario.DescricaoMaxLength);
        builder.Property(modelo => modelo.Finalidade).HasConversion(FinalidadeConverter).HasMaxLength(30).IsRequired();
        builder.Property(modelo => modelo.TipoProcessoCodigo).HasMaxLength(ModeloFormulario.TipoProcessoCodigoMaxLength);
        builder.Property(modelo => modelo.Ativo).IsRequired();

        // Etapas, itens, termos e pressupostos são lidos e gravados sempre inteiros, junto do modelo,
        // sem consulta por item — num documento, e não em linhas (ADR-0137). Mudar só o conteúdo muda
        // a linha do modelo, e o xmin abaixo confere a concorrência dessa edição também.
        builder.Property(modelo => modelo.Conteudo)
            .HasColumnType("jsonb")
            .HasConversion(ConteudoDoModeloJson.Converter, ConteudoDoModeloJson.Comparer)
            .IsRequired();

        // Concorrência otimista pela coluna de sistema xmin (convenção do provider Npgsql).
        builder.Property<uint>("Version").IsRowVersion();

        builder.Property(modelo => modelo.CreatedBy).HasMaxLength(255);
        builder.Property(modelo => modelo.UpdatedBy).HasMaxLength(255);

        // O código é chave natural imutável e nunca reutilizado: o modelo é desativado, não apagado.
        builder.HasIndex(modelo => modelo.Codigo).IsUnique().HasDatabaseName("ix_modelos_formulario_codigo");
        builder.HasIndex(modelo => new { modelo.TipoProcessoCodigo, modelo.Finalidade, modelo.Ativo })
            .HasDatabaseName("ix_modelos_formulario_tipo_finalidade_ativo");
    }
}
