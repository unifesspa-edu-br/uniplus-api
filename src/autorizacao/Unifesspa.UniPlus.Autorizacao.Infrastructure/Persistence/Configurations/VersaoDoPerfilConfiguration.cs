namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das versões do perfil, das permissões que cada uma reúne e das vigências.</summary>
public sealed class VersaoDoPerfilConfiguration : IEntityTypeConfiguration<VersaoDoPerfil>
{
    private const int NomeMaxLength = 120;
    private const int DescricaoMaxLength = 500;
    private const int ProtocoloMaxLength = 60;
    private const int CodigoDaPermissaoMaxLength = 120;
    private const int SituacaoMaxLength = 20;
    private const int CodigoMaxLength = 40;
    private const int ClassificacaoMaxLength = 20;
    private const int IdentidadeMaxLength = 255;

    private static readonly ValueConverter<SituacaoDaVersao, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDaVersaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<OrigemDaVersao, string> OrigemConverter = new(
        origem => origem.ToCodigo(),
        codigo => OrigemDaVersaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<TipoDeEscopo, string> AlcanceConverter = new(
        alcance => alcance.ToCodigo(),
        codigo => TipoDeEscopoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<FormaDoCancelamento, string> FormaConverter = new(
        forma => forma.ToCodigo(),
        codigo => FormaDoCancelamentoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<CategoriaDaRemocao, string> CategoriaConverter = new(
        categoria => categoria.ToCodigo(),
        codigo => CategoriaDaRemocaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<ClassificacaoDeProtecao, string> ClassificacaoConverter = new(
        classificacao => classificacao.ToString().ToLowerInvariant(),
        codigo => Enum.Parse<ClassificacaoDeProtecao>(codigo, true));

    public void Configure(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("versoes_do_perfil", ConfigurarRestricoes);
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        ConfigurarConteudo(builder);
        ConfigurarSituacao(builder);
        ConfigurarAutoria(builder);
        ConfigurarCancelamentoEExpiracao(builder);
        ConfigurarRemocao(builder);
        ConfigurarDecisoes(builder);
        ConfigurarPermissoes(builder);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }

    private static void ConfigurarConteudo(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.Property(v => v.Numero).IsRequired();
        builder.Property(v => v.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(v => v.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();
        builder.Property(v => v.Protocolo).HasMaxLength(ProtocoloMaxLength).IsRequired();

        // A vigência aponta para a versão pelo par (perfil, versão): o banco recusa vigência de uma versão que é de outro perfil.
        builder.HasAlternateKey(v => new { v.PerfilId, v.Id }).HasName("ak_versoes_do_perfil_perfil_versao");
        builder.HasIndex(v => new { v.PerfilId, v.Numero }).IsUnique().HasDatabaseName("ux_versoes_do_perfil_numero");
        builder.HasMany(v => v.Documentos)
            .WithOne()
            .HasForeignKey(d => new { d.PerfilId, d.VersaoId })
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Documentos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(v => v.Origem).HasConversion(OrigemConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(v => v.Alcance).HasConversion(AlcanceConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(v => v.ExpiraEm);
        builder.Property(v => v.ExigeProtecaoDeDados).IsRequired();
        builder.Property(v => v.CicloDeAprovacao).IsRequired();
    }

    // Uma pendente e uma vigente por perfil: a trava do perfil serializa, e o índice parcial barra o que escapar dela.
    private static void ConfigurarSituacao(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.Property(v => v.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();

        builder.HasIndex(v => v.PerfilId, "ux_versoes_do_perfil_pendente")
            .IsUnique()
            .HasFilter($"situacao = '{SituacaoDaVersaoCodigo.Pendente}'")
            .HasDatabaseName("ux_versoes_do_perfil_pendente");
        builder.HasIndex(v => v.PerfilId, "ux_versoes_do_perfil_vigente")
            .IsUnique()
            .HasFilter($"situacao = '{SituacaoDaVersaoCodigo.Vigente}'")
            .HasDatabaseName("ux_versoes_do_perfil_vigente");
    }

    private static void ConfigurarAutoria(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.Property(v => v.CriadaPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(v => v.CriadaPorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(v => v.CriadaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(v => v.CriadaPorEmail).HasMaxLength(PedidoDeConcessao.EmailMaxLength).ClassificaComo(ClassificacaoDeProtecao.Pessoal);
        builder.Property(v => v.CriadorTemConcessao).IsRequired();
        builder.Ignore(v => v.CriadaPor);
        builder.Property(v => v.CanceladaPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(v => v.CanceladaPorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(v => v.CanceladaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
    }

    private static void ConfigurarCancelamentoEExpiracao(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.Property(v => v.FormaDoCancelamento).HasConversion(FormaConverter).HasMaxLength(CodigoMaxLength);
        builder.Property(v => v.MotivoDoCancelamento).HasMaxLength(MotivosDaVersao.MaxLength);
        builder.Property(v => v.ExpiradaEm);
    }

    // A remoção aponta para a versão proposta que tem o documento pelo par (perfil, versão): o banco recusa o documento de
    // outro perfil. A FK de versão para versão é restrita: nenhuma versão some por exclusão em cascata.
    private static void ConfigurarRemocao(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.Property(v => v.MotivoDaRemocao).HasMaxLength(MotivosDaVersao.MaxLength);
        builder.Property(v => v.CategoriaDaRemocao).HasConversion(CategoriaConverter).HasMaxLength(CodigoMaxLength);
        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(v => new { v.PerfilId, v.VersaoDoDocumento })
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurarDecisoes(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.HasMany(v => v.Decisoes).WithOne().HasForeignKey(d => d.VersaoId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(v => v.Decisoes).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(v => v.Recalculos).WithOne().HasForeignKey(r => r.VersaoId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(v => v.Recalculos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarRestricoes(TableBuilder<VersaoDoPerfil> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_versoes_do_perfil_origem",
            $"(origem = '{OrigemDaVersaoCodigo.Remocao}' AND motivo_da_remocao IS NOT NULL AND versao_do_documento IS NOT NULL AND expira_em IS NULL) "
            + $"OR (origem = '{OrigemDaVersaoCodigo.Proposta}' AND expira_em IS NOT NULL AND motivo_da_remocao IS NULL AND versao_do_documento IS NULL)");
        tabela.HasCheckConstraint(
            "ck_versoes_do_perfil_cancelamento",
            $"(situacao = '{SituacaoDaVersaoCodigo.Cancelada}') = (forma_do_cancelamento IS NOT NULL AND cancelada_em IS NOT NULL) "
            + "AND (forma_do_cancelamento IS NULL) = (cancelada_em IS NULL)");
        tabela.HasCheckConstraint(
            "ck_versoes_do_perfil_motivo_do_cancelamento",
            $"forma_do_cancelamento IS DISTINCT FROM '{FormaDoCancelamentoCodigo.Pessoa}' OR motivo_do_cancelamento IS NOT NULL");
        tabela.HasCheckConstraint(
            "ck_versoes_do_perfil_expiracao",
            $"(situacao = '{SituacaoDaVersaoCodigo.Expirada}') = (expirada_em IS NOT NULL)");
    }

    private static void ConfigurarPermissoes(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.Navigation(v => v.Permissoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(v => v.Permissoes, permissao =>
        {
            permissao.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            permissao.ToTable("versao_do_perfil_permissoes");
            permissao.WithOwner().HasForeignKey("VersaoId");
            permissao.Property<Guid>("VersaoId");
            permissao.Property(x => x.Codigo).HasMaxLength(CodigoDaPermissaoMaxLength).IsRequired();
            permissao.Property(x => x.Classificacao).HasConversion(ClassificacaoConverter).HasMaxLength(ClassificacaoMaxLength).IsRequired();
            permissao.Property(x => x.DeUnidade).IsRequired();
            permissao.HasKey("VersaoId", nameof(PermissaoDaVersao.Codigo));
        });
    }
}
