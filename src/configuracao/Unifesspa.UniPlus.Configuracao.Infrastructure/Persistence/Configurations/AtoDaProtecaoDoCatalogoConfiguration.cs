namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core dos atos da proteção de dados do catálogo, registro só acrescentado: sem chave
/// estrangeira para o item, que pode ser desativado sem apagar o registro. As colunas de quem agiu são
/// <c>Pessoal</c>, como em <c>pedido_decisoes</c>; as demais são <c>Interno</c>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AtoDaProtecaoDoCatalogoConfiguration : IEntityTypeConfiguration<AtoDaProtecaoDoCatalogo>
{
    private const int CadastroMaxLength = 20;
    private const int AtoMaxLength = 20;

    private static readonly ValueConverter<CadastroDoCatalogo, string> CadastroConverter =
        new(cadastro => CadastrosDoCatalogo.ParaTokenCanonico(cadastro), token => CadastrosDoCatalogo.Analisar(token));

    private static readonly ValueConverter<AtoDaProtecao, string> AtoConverter =
        new(ato => AtosDaProtecao.ParaTokenCanonico(ato), token => AtosDaProtecao.Analisar(token));

    public void Configure(EntityTypeBuilder<AtoDaProtecaoDoCatalogo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("protecao_do_catalogo_atos", ConfigurarChecks);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Cadastro).HasConversion(CadastroConverter).HasMaxLength(CadastroMaxLength).IsRequired();
        builder.Property(a => a.ItemId).IsRequired();
        builder.Property(a => a.ItemCodigo).HasMaxLength(AtoDaProtecaoDoCatalogo.ItemCodigoMaxLength).IsRequired();
        builder.Property(a => a.ItemNome).HasMaxLength(AtoDaProtecaoDoCatalogo.ItemNomeMaxLength).IsRequired();
        builder.Property(a => a.Ato).HasConversion(AtoConverter).HasMaxLength(AtoMaxLength).IsRequired();

        builder.Property(a => a.ClassificacaoProtecao)
            .HasConversion<ClassificacaoDeProtecaoValueConverter>()
            .HasMaxLength(ClassificacaoDeProtecaoValueConverter.MaxLength)
            .IsRequired();
        builder.Property(a => a.HipoteseLegal)
            .HasConversion(ProtecaoDeDadosConversores.HipoteseLegal)
            .HasMaxLength(ProtecaoDeDadosConversores.HipoteseLegalMaxLength)
            .IsRequired();
        builder.Property(a => a.FinalidadeTratamento).HasMaxLength(ProtecaoDeDados.FinalidadeMaxLength).IsRequired();

        builder.Property(a => a.ClassificacaoRecomendada)
            .HasConversion<ClassificacaoDeProtecaoValueConverter>()
            .HasMaxLength(ClassificacaoDeProtecaoValueConverter.MaxLength);
        builder.Property(a => a.Justificativa).HasMaxLength(AprovacaoDaProtecao.JustificativaMaxLength);

        builder.Property(a => a.AtorEmissor).HasMaxLength(AtorDoAto.IdentidadeMaxLength).IsRequired()
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);
        builder.Property(a => a.AtorSub).HasMaxLength(AtorDoAto.IdentidadeMaxLength).IsRequired()
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);
        builder.Property(a => a.AtorNome).HasMaxLength(AtorDoAto.NomeMaxLength).ClassificaComo(ClassificacaoDeProtecao.Pessoal);
        builder.Property(a => a.Instante).IsRequired();

        builder.HasIndex(a => new { a.Cadastro, a.ItemId, a.Instante }).HasDatabaseName("ix_protecao_do_catalogo_atos_item");
        builder.HasIndex(a => new { a.Ato, a.Instante }).HasDatabaseName("ix_protecao_do_catalogo_atos_ato");
    }

    private static void ConfigurarChecks(TableBuilder<AtoDaProtecaoDoCatalogo> tabela)
    {
        string cadastros = string.Join(", ", CadastrosDoCatalogo.TokensCanonicos.Select(token => $"'{token}'"));
        string atos = string.Join(", ", AtosDaProtecao.TokensCanonicos.Select(token => $"'{token}'"));

        tabela.HasCheckConstraint("ck_protecao_do_catalogo_atos_cadastro", $"cadastro IN ({cadastros})");
        tabela.HasCheckConstraint("ck_protecao_do_catalogo_atos_ato", $"ato IN ({atos})");
        tabela.HasCheckConstraint(
            "ck_protecao_do_catalogo_atos_ajuste_com_recomendacao",
            "ato <> 'AJUSTE_RECOMENDADO' OR classificacao_recomendada IS NOT NULL");
        tabela.HasCheckConstraint(
            "ck_protecao_do_catalogo_atos_reprovacao_com_justificativa",
            "ato <> 'REPROVACAO' OR justificativa IS NOT NULL");
    }
}
