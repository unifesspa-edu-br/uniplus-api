namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Configuração EF Core de <see cref="FormularioProcesso"/> (UNI-REQ-0144): filho de
/// <c>ProcessoSeletivo</c>, no máximo um por finalidade, com as páginas em cascata.
/// </summary>
public sealed class FormularioProcessoConfiguration : IEntityTypeConfiguration<FormularioProcesso>
{
    public void Configure(EntityTypeBuilder<FormularioProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("formularios_processo", t =>
            t.HasCheckConstraint("ck_formularios_processo_finalidade", $"finalidade IN ({SqlDosCodigosDeContrato.Lista<FinalidadeFormulario>()})"));
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Finalidade).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired().HasComment("Finalidade do formulário.");
        builder.Property(f => f.Titulo).HasMaxLength(FormaDoCabecalho.TituloMaxLength);
        builder.Property(f => f.ModeloOrigemCodigo).HasMaxLength(FormularioProcesso.ModeloOrigemCodigoMaxLength);
        builder.Property(f => f.ModeloOrigemNome).HasColumnName("modelo_origem_nome").HasMaxLength(FormularioProcesso.ModeloOrigemNomeMaxLength)
            .HasComment("Nome copiado do modelo de formulário de origem, nulo quando não há modelo.");
        builder.Ignore(f => f.Estrutura);

        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Finalidade })
            .IsUnique()
            .HasDatabaseName("ux_formularios_processo_processo_finalidade");

        builder.HasMany(f => f.Paginas)
            .WithOne()
            .HasForeignKey(e => e.FormularioProcessoId)
            .HasConstraintName("fk_paginas_do_formulario_formulario_processo")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Paginas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Configuração EF Core de <see cref="PaginaDoFormulario"/>: código e ordem únicos no formulário.</summary>
public sealed class PaginaDoFormularioConfiguration : IEntityTypeConfiguration<PaginaDoFormulario>
{
    public void Configure(EntityTypeBuilder<PaginaDoFormulario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("paginas_do_formulario", t =>
        {
            t.HasCheckConstraint("ck_paginas_do_formulario_tipo", $"tipo IN ({SqlDosCodigosDeContrato.Lista<TipoPaginaFormulario>()})");
            t.HasCheckConstraint("ck_paginas_do_formulario_bloco", $"bloco IS NULL OR bloco IN ({SqlDosCodigosDeContrato.Lista<BlocoSistema>()})");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Codigo).HasMaxLength(FormaDaPagina.CodigoMaxLength).IsRequired();
        builder.Property(e => e.Ordem).IsRequired();
        builder.Property(e => e.Tipo).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired()
            .HasComment("O que a página é.");
        builder.Property(e => e.Bloco).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo)
            .HasComment("O bloco que o sistema monta; nulo na seção.");
        builder.Property(e => e.Titulo).HasMaxLength(FormaDaPagina.TituloMaxLength).IsRequired();
        builder.Property(e => e.Descricao).HasMaxLength(FormaDaPagina.TextoMaxLength);
        builder.Property(e => e.Aviso).HasMaxLength(FormaDaPagina.TextoMaxLength);
        builder.Property(e => e.Exibicao)
            .HasConversion(ConversoresDeRegras.Predicado, ConversoresDeRegras.ComparadorDePredicado)
            .HasColumnType("jsonb");
        builder.Ignore(e => e.Estrutura);
        builder.Ignore(e => e.FatosCitados);
        builder.Ignore(e => e.Condicoes);

        builder.HasIndex(e => new { e.FormularioProcessoId, e.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_paginas_do_formulario_formulario_codigo");
        builder.HasIndex(e => new { e.FormularioProcessoId, e.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_paginas_do_formulario_formulario_ordem");
    }
}
