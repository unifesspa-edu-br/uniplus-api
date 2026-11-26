namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core das bases das permissões nas concessões, registro só acrescentado. O banco recusa a saída com
/// classificação ou escopo e a base viva sem eles.
/// </summary>
internal sealed class BaseDaPermissaoNaConcessaoConfiguration : IEntityTypeConfiguration<BaseDaPermissaoNaConcessao>
{
    private const int CodigoMaxLength = 120;
    private const int ClassificacaoMaxLength = 20;
    private const int OrigemMaxLength = 20;

    public void Configure(EntityTypeBuilder<BaseDaPermissaoNaConcessao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("bases_da_permissao_concedida", ConfigurarRestricoes);
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Codigo).HasConversion(ConversoresDoCatalogo.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(b => b.Classificacao).HasMaxLength(ClassificacaoMaxLength);
        builder.Property(b => b.DeUnidade);
        builder.Property(b => b.Origem).HasMaxLength(OrigemMaxLength).IsRequired();
        builder.Property(b => b.ReferenciaId).IsRequired();
        builder.Property(b => b.OrdemNoHistorico).IsRequired();
        builder.Ignore(b => b.OrdinalLocal);

        // A concessão pertence ao perfil: o banco recusa a base de uma concessão que é de outro perfil.
        builder.HasOne<Concessao>()
            .WithMany(c => c.Bases)
            .HasForeignKey(b => new { b.PerfilId, b.ConcessaoId })
            .HasConstraintName("fk_bases_da_permissao_concedida_concessao")
            .HasPrincipalKey(c => new { c.PerfilId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.ConcessaoId, b.Codigo, b.OrdemNoHistorico })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_bases_da_permissao_concedida_concessao_codigo");
        builder.HasIndex(b => new { b.PerfilId, b.ConcessaoId }).HasDatabaseName("ix_bases_da_permissao_concedida_perfil_concessao");
        builder.HasIndex(b => b.OrdemNoHistorico).IsUnique().HasDatabaseName("ux_bases_da_permissao_concedida_ordem");
    }

    private static void ConfigurarRestricoes(TableBuilder<BaseDaPermissaoNaConcessao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_bases_da_permissao_concedida_origem",
            $"origem IN ({SqlDosCodigosDeContrato.Lista<OrigemDaBase>()})");
        tabela.HasCheckConstraint(
            "ck_bases_da_permissao_concedida_saida",
            $"(origem = {SqlDosCodigosDeContrato.Literal(OrigemDaBase.Saida)}) = (classificacao IS NULL AND de_unidade IS NULL)");
    }
}
