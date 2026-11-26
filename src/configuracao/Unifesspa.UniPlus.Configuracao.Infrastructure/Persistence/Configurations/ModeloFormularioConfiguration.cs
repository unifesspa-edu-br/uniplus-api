namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class ModeloFormularioConfiguration : IEntityTypeConfiguration<ModeloFormulario>
{
    public void Configure(EntityTypeBuilder<ModeloFormulario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("modelos_formulario", static tabela =>
        {
            tabela.HasCheckConstraint("ck_modelos_formulario_codigo_formato", $"codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
            tabela.HasCheckConstraint("ck_modelos_formulario_tipo_processo_codigo_formato", $"tipo_processo_codigo IS NULL OR tipo_processo_codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");
            tabela.HasComment("Modelos de formulário e seu conteúdo para processos seletivos.");
        });
        builder.HasKey(modelo => modelo.Id);
        builder.Property(modelo => modelo.Id).ValueGeneratedNever().HasComment("Identificador do modelo de formulário.");
        builder.Property(modelo => modelo.Codigo).HasMaxLength(ModeloFormulario.CodigoMaxLength).IsRequired().HasComment("Código que identifica o modelo de formulário.");
        builder.Property(modelo => modelo.Nome).HasMaxLength(ModeloFormulario.NomeMaxLength).IsRequired().HasComment("Nome do modelo de formulário.");
        builder.Property(modelo => modelo.Descricao).HasMaxLength(ModeloFormulario.DescricaoMaxLength).HasComment("Descrição do modelo de formulário.");
        builder.Property(modelo => modelo.ServeAFaseDeInscricao).IsRequired().HasComment("Indica se o modelo serve à fase que coleta a inscrição e traz o conjunto básico de dados do candidato.");
        builder.Property(modelo => modelo.Obrigatorio).IsRequired().HasComment("Indica se o modelo é obrigatório no processo em que for aplicado.");
        builder.Property(modelo => modelo.TipoProcessoCodigo).HasMaxLength(ModeloFormulario.TipoProcessoCodigoMaxLength).HasComment("Código do tipo de processo ao qual o modelo se aplica.");
        builder.Property(modelo => modelo.Ativo).IsRequired().HasComment("Indica se o modelo está disponível para uso.");

        // Páginas, itens, termos e pressupostos são lidos e gravados sempre inteiros, junto do modelo,
        // sem consulta por item — num documento, e não em linhas (ADR-0137). Mudar só o conteúdo muda
        // a linha do modelo, e o xmin abaixo confere a concorrência dessa edição também.
        builder.Property(modelo => modelo.Conteudo)
            .HasColumnType("jsonb")
            .HasConversion(ConteudoDoModeloJson.Converter, ConteudoDoModeloJson.Comparer)
            .IsRequired()
            .HasComment("Conteúdo do modelo de formulário, com páginas, itens, termos e pressupostos.");

        // Concorrência otimista pela coluna de sistema xmin (convenção do provider Npgsql).
        builder.Property<uint>("Version").IsRowVersion();

        builder.Property(modelo => modelo.CreatedAt).HasComment("Data e hora de criação do modelo.");
        builder.Property(modelo => modelo.CreatedBy).HasMaxLength(255).HasComment("Identificação de quem criou o modelo.");
        builder.Property(modelo => modelo.UpdatedAt).HasComment("Data e hora da última atualização do modelo.");
        builder.Property(modelo => modelo.UpdatedBy).HasMaxLength(255).HasComment("Identificação de quem atualizou o modelo.");

        // O código é chave natural imutável e nunca reutilizado: o modelo é desativado, não apagado.
        builder.HasIndex(modelo => modelo.Codigo).IsUnique().HasDatabaseName("ux_modelos_formulario_codigo");
        builder.HasIndex(modelo => new { modelo.TipoProcessoCodigo, modelo.ServeAFaseDeInscricao, modelo.Ativo })
            .HasDatabaseName("ix_modelos_formulario_tipo_serve_inscricao_ativo");
    }
}
