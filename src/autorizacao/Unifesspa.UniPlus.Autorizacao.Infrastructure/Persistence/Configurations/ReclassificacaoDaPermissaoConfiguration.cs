namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core das reclassificações das permissões nos perfis, registro só acrescentado. O banco recusa a linha
/// em que nenhum eixo mudou e a repetição do fato no mesmo catálogo.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ReclassificacaoDaPermissaoConfiguration : IEntityTypeConfiguration<ReclassificacaoDaPermissao>
{
    private const int CodigoMaxLength = 120;
    private const int ClassificacaoMaxLength = 20;

    public void Configure(EntityTypeBuilder<ReclassificacaoDaPermissao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("reclassificacoes_da_permissao", tabela => tabela.HasCheckConstraint(
            "ck_reclassificacoes_da_permissao_mudanca",
            "classificacao_anterior <> classificacao_nova OR de_unidade_anterior <> de_unidade_novo"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(r => r.ClassificacaoAnterior).HasConversion(ConversoresDoCatalogo.Classificacao).HasMaxLength(ClassificacaoMaxLength).IsRequired();
        builder.Property(r => r.ClassificacaoNova).HasConversion(ConversoresDoCatalogo.Classificacao).HasMaxLength(ClassificacaoMaxLength).IsRequired();
        builder.Property(r => r.DeUnidadeAnterior).IsRequired();
        builder.Property(r => r.DeUnidadeNovo).IsRequired();
        builder.Property(r => r.Instante).IsRequired();
        builder.Property(r => r.OrdemNaTrilha).IsRequired();
        builder.Ignore(r => r.OrdinalLocal);
        builder.Ignore(r => r.Subiu);
        builder.Ignore(r => r.Desceu);
        builder.Ignore(r => r.MudouEscopo);

        builder.HasOne<ImplantacaoDasPermissoes>().WithMany().HasForeignKey(r => r.ImplantacaoId)
            .HasConstraintName("fk_reclassificacoes_da_permissao_implantacao").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Perfil>().WithMany().HasForeignKey(r => r.PerfilId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.ImplantacaoId, r.PerfilId, r.Codigo }).IsUnique().HasDatabaseName("ux_reclassificacoes_da_permissao_fato");
        builder.HasIndex(r => new { r.PerfilId, r.Codigo, r.OrdemNaTrilha })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_reclassificacoes_da_permissao_perfil_codigo");
        builder.HasIndex(r => r.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_reclassificacoes_da_permissao_ordem");
    }
}
