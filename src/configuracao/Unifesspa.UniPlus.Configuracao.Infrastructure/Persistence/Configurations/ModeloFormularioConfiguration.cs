namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

public sealed class ModeloFormularioConfiguration : IEntityTypeConfiguration<ModeloFormulario>
{
    private static readonly ValueConverter<FinalidadeFormulario, string> FinalidadeConverter =
        new(finalidade => EstruturaFormulario.ParaToken(finalidade), token => EstruturaFormulario.FinalidadeDoToken(token));

    public void Configure(EntityTypeBuilder<ModeloFormulario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        // Domínio fechado da finalidade (defesa em profundidade contra inserts crus).
        builder.ToTable("modelos_formulario", static tabela =>
        {
            tabela.HasComment("Modelos de formulário e seu conteúdo para processos seletivos.");
            tabela.HasCheckConstraint(
                "ck_modelos_formulario_finalidade",
                $"finalidade IN ('{EstruturaFormulario.FinalidadeInscricao}', '{EstruturaFormulario.FinalidadeIsencaoTaxa}', '{EstruturaFormulario.FinalidadeHabilitacao}')");
        });
        builder.HasKey(modelo => modelo.Id);
        builder.Property(modelo => modelo.Id).ValueGeneratedNever().HasComment("Identificador do modelo de formulário.");
        builder.Property(modelo => modelo.Codigo).HasMaxLength(ModeloFormulario.CodigoMaxLength).IsRequired().HasComment("Código que identifica o modelo de formulário.");
        builder.Property(modelo => modelo.Nome).HasMaxLength(ModeloFormulario.NomeMaxLength).IsRequired().HasComment("Nome do modelo de formulário.");
        builder.Property(modelo => modelo.Descricao).HasMaxLength(ModeloFormulario.DescricaoMaxLength).HasComment("Descrição do modelo de formulário.");
        builder.Property(modelo => modelo.Finalidade).HasConversion(FinalidadeConverter).HasMaxLength(30).IsRequired().HasComment("Finalidade do formulário: INSCRICAO, ISENCAO_TAXA ou HABILITACAO.");
        builder.Property(modelo => modelo.TipoProcessoCodigo).HasMaxLength(ModeloFormulario.TipoProcessoCodigoMaxLength).HasComment("Código do tipo de processo ao qual o modelo se aplica.");
        builder.Property(modelo => modelo.Ativo).IsRequired().HasComment("Indica se o modelo está disponível para uso.");

        // Etapas, itens, termos e pressupostos são lidos e gravados sempre inteiros, junto do modelo,
        // sem consulta por item — num documento, e não em linhas (ADR-0137). Mudar só o conteúdo muda
        // a linha do modelo, e o xmin abaixo confere a concorrência dessa edição também.
        builder.Property(modelo => modelo.Conteudo)
            .HasColumnType("jsonb")
            .HasConversion(ConteudoDoModeloJson.Converter, ConteudoDoModeloJson.Comparer)
            .IsRequired()
            .HasComment("Conteúdo do modelo de formulário, com etapas, itens, termos e pressupostos.");

        // Concorrência otimista pela coluna de sistema xmin (convenção do provider Npgsql).
        builder.Property<uint>("Version").IsRowVersion();

        builder.Property(modelo => modelo.CreatedAt).HasComment("Data e hora de criação do modelo.");
        builder.Property(modelo => modelo.CreatedBy).HasMaxLength(255).HasComment("Identificação de quem criou o modelo.");
        builder.Property(modelo => modelo.UpdatedAt).HasComment("Data e hora da última atualização do modelo.");
        builder.Property(modelo => modelo.UpdatedBy).HasMaxLength(255).HasComment("Identificação de quem atualizou o modelo.");

        // O código é chave natural imutável e nunca reutilizado: o modelo é desativado, não apagado.
        builder.HasIndex(modelo => modelo.Codigo).IsUnique().HasDatabaseName("ix_modelos_formulario_codigo");
        builder.HasIndex(modelo => new { modelo.TipoProcessoCodigo, modelo.Finalidade, modelo.Ativo })
            .HasDatabaseName("ix_modelos_formulario_tipo_finalidade_ativo");
    }
}
