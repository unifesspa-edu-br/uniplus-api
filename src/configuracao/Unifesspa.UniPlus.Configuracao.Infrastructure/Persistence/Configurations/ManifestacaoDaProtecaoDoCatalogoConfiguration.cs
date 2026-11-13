namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core das manifestações técnicas na proteção do catálogo (ADR-0163), registro só acrescentado: sem
/// chave estrangeira para o item, como o ato. As colunas de quem se manifestou são <c>Pessoal</c>; as demais, inclusive
/// o texto, são <c>Interno</c>, como a justificativa da decisão.
/// </summary>
/// <remarks>
/// Os índices únicos guardam uma única cadeia por pessoa e ciclo: no máximo uma manifestação inicial por item, ciclo e
/// pessoa, e no máximo uma substituição de cada manifestação. São defesa em profundidade: a mudança na linha do item já
/// serializa as manifestações pelo token de concorrência.
/// </remarks>
public sealed class ManifestacaoDaProtecaoDoCatalogoConfiguration : IEntityTypeConfiguration<ManifestacaoDaProtecaoDoCatalogo>
{
    /// <summary>Nome do campo do cadastro do item, mapeado na coluna <c>cadastro</c>.</summary>
    internal const string CampoDoCadastro = "_cadastro";

    /// <summary>Nome do campo do identificador do item, mapeado na coluna <c>item_id</c>.</summary>
    internal const string CampoDoItem = "_itemId";

    private const string CampoDoEmissor = "_autorEmissor";
    private const string CampoDoSub = "_autorSub";
    private const string CampoDoNome = "_autorNome";
    private const int CadastroMaxLength = 20;

    private static readonly ValueConverter<CadastroDoCatalogo, string> CadastroConverter =
        new(cadastro => CadastrosDoCatalogo.ParaTokenCanonico(cadastro), token => CadastrosDoCatalogo.Analisar(token));

    public void Configure(EntityTypeBuilder<ManifestacaoDaProtecaoDoCatalogo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("protecao_do_catalogo_manifestacoes", ConfigurarChecks);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Ignore(m => m.Item);
        builder.Ignore(m => m.Autor);
        builder.Ignore(m => m.Desfavoravel);

        builder.Property<CadastroDoCatalogo>(CampoDoCadastro).HasColumnName("cadastro")
            .HasConversion(CadastroConverter).HasMaxLength(CadastroMaxLength).IsRequired();
        builder.Property<Guid>(CampoDoItem).HasColumnName("item_id").IsRequired();
        builder.Property(m => m.Ciclo).HasConversion(ManifestacaoDaProtecaoConversores.Ciclo).IsRequired();
        builder.Property(m => m.Parecer)
            .HasConversion(ManifestacaoDaProtecaoConversores.Parecer)
            .HasMaxLength(ManifestacaoDaProtecaoConversores.ParecerMaxLength)
            .IsRequired();
        builder.Property(m => m.Texto).HasConversion(ManifestacaoDaProtecaoConversores.Texto).HasMaxLength(Fundamentacao.MaxLength);
        builder.Property(m => m.Instante).IsRequired();

        builder.Property<string>(CampoDoEmissor).HasColumnName("autor_emissor").HasMaxLength(AtorDoAto.IdentidadeMaxLength).IsRequired()
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);
        builder.Property<string>(CampoDoSub).HasColumnName("autor_sub").HasMaxLength(AtorDoAto.IdentidadeMaxLength).IsRequired()
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);
        builder.Property<string?>(CampoDoNome).HasColumnName("autor_nome").HasMaxLength(AtorDoAto.NomeMaxLength)
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);

        // A substituição aponta para a manifestação substituída, que nunca é apagada (registro só acrescentado).
        builder.HasOne<ManifestacaoDaProtecaoDoCatalogo>()
            .WithOne()
            .HasForeignKey<ManifestacaoDaProtecaoDoCatalogo>(m => m.SubstituiId)
            .HasConstraintName("fk_protecao_do_catalogo_manifestacoes_substitui")
            .OnDelete(DeleteBehavior.Restrict);

        ConfigurarIndices(builder);
    }

    private static void ConfigurarIndices(EntityTypeBuilder<ManifestacaoDaProtecaoDoCatalogo> builder)
    {
        builder.HasIndex(CampoDoCadastro, CampoDoItem, nameof(ManifestacaoDaProtecaoDoCatalogo.Ciclo), nameof(ManifestacaoDaProtecaoDoCatalogo.Instante))
            .HasDatabaseName("ix_protecao_do_catalogo_manifestacoes_item_ciclo");
        builder.HasIndex(m => m.SubstituiId)
            .IsUnique()
            .HasDatabaseName("ux_protecao_do_catalogo_manifestacoes_substitui");
        builder.HasIndex(CampoDoCadastro, CampoDoItem, nameof(ManifestacaoDaProtecaoDoCatalogo.Ciclo), CampoDoEmissor, CampoDoSub)
            .IsUnique()
            .HasFilter("substitui_id IS NULL")
            .HasDatabaseName("ux_protecao_do_catalogo_manifestacoes_inicial_do_autor");
    }

    private static void ConfigurarChecks(TableBuilder<ManifestacaoDaProtecaoDoCatalogo> tabela)
    {
        string cadastros = string.Join(", ", CadastrosDoCatalogo.TokensCanonicos.Select(token => $"'{token}'"));
        string pareceres = string.Join(", ", PareceresDaManifestacao.TokensCanonicos.Select(token => $"'{token}'"));

        tabela.HasCheckConstraint("ck_protecao_do_catalogo_manifestacoes_cadastro", $"cadastro IN ({cadastros})");
        tabela.HasCheckConstraint("ck_protecao_do_catalogo_manifestacoes_parecer", $"parecer IN ({pareceres})");
        tabela.HasCheckConstraint("ck_protecao_do_catalogo_manifestacoes_ciclo_positivo", "ciclo >= 1");
        tabela.HasCheckConstraint(
            "ck_protecao_do_catalogo_manifestacoes_texto_no_desfavoravel", "parecer = 'ADEQUADA' OR texto IS NOT NULL");
        tabela.HasCheckConstraint(
            "ck_protecao_do_catalogo_manifestacoes_substitui_outra", "substitui_id IS NULL OR substitui_id <> id");
    }
}
