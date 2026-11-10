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
/// Configuração EF Core das bases das permissões nas concessões, registro só acrescentado. O banco recusa a saída com
/// classificação ou escopo e a base viva sem eles.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class BaseDaPermissaoNaConcessaoConfiguration : IEntityTypeConfiguration<BaseDaPermissaoNaConcessao>
{
    private const int CodigoMaxLength = 120;
    private const int ClassificacaoMaxLength = 20;
    private const int OrigemMaxLength = 20;

    private static readonly ValueConverter<OrigemDaBase, string> OrigemConverter = new(
        origem => origem.ToCodigo(),
        codigo => OrigemDaBaseCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<BaseDaPermissaoNaConcessao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("bases_da_permissao_na_concessao", ConfigurarRestricoes);
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(b => b.Classificacao).HasConversion(ConversoresDoCatalogo.ClassificacaoOpcional).HasMaxLength(ClassificacaoMaxLength);
        builder.Property(b => b.DeUnidade);
        builder.Property(b => b.Origem).HasConversion(OrigemConverter).HasMaxLength(OrigemMaxLength).IsRequired();
        builder.Property(b => b.ReferenciaId).IsRequired();
        builder.Property(b => b.OrdemNaTrilha).IsRequired();
        builder.Ignore(b => b.OrdinalLocal);

        // A concessão pertence ao perfil: o banco recusa a base de uma concessão que é de outro perfil.
        builder.HasOne<Concessao>()
            .WithMany(c => c.Bases)
            .HasForeignKey(b => new { b.PerfilId, b.ConcessaoId })
            .HasPrincipalKey(c => new { c.PerfilId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.ConcessaoId, b.Codigo, b.OrdemNaTrilha })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_bases_da_permissao_concessao_codigo");
        builder.HasIndex(b => b.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_bases_da_permissao_ordem");
    }

    private static void ConfigurarRestricoes(TableBuilder<BaseDaPermissaoNaConcessao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_bases_da_permissao_origem",
            $"origem IN ('{OrigemDaBaseCodigo.Migracao}', '{OrigemDaBaseCodigo.Saida}', '{OrigemDaBaseCodigo.Liberacao}', '{OrigemDaBaseCodigo.Conciliacao}')");
        tabela.HasCheckConstraint(
            "ck_bases_da_permissao_saida",
            $"(origem = '{OrigemDaBaseCodigo.Saida}') = (classificacao IS NULL AND de_unidade IS NULL)");
    }
}
