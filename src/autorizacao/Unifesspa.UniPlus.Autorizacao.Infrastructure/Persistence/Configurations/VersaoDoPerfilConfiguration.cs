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
    private const int CodigoMaxLength = 120;
    private const int SituacaoMaxLength = 20;
    private const int MotivoMaxLength = 60;
    private const int ClassificacaoMaxLength = 20;
    private const int IdentidadeMaxLength = 255;

    private static readonly ValueConverter<SituacaoDaVersao, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDaVersaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<MotivoDaRecusaDaVersao, string> MotivoConverter = new(
        motivo => motivo.ToCodigo(),
        codigo => MotivoDaRecusaDaVersaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<ClassificacaoDeProtecao, string> ClassificacaoConverter = new(
        classificacao => classificacao.ToString().ToLowerInvariant(),
        codigo => Enum.Parse<ClassificacaoDeProtecao>(codigo, true));

    public void Configure(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("versoes_do_perfil");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        ConfigurarConteudo(builder);
        ConfigurarSituacao(builder);
        ConfigurarAutoria(builder);
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
    }

    // Uma pendente e uma vigente por perfil: a trava do perfil serializa, e o índice parcial barra o que escapar dela.
    private static void ConfigurarSituacao(EntityTypeBuilder<VersaoDoPerfil> builder)
    {
        builder.Property(v => v.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(v => v.MotivoDaRecusa).HasConversion(MotivoConverter).HasMaxLength(MotivoMaxLength);

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
        builder.Property(v => v.CanceladaPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(v => v.CanceladaPorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(v => v.CanceladaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
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
            permissao.Property(x => x.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
            permissao.Property(x => x.Classificacao).HasConversion(ClassificacaoConverter).HasMaxLength(ClassificacaoMaxLength).IsRequired();
            permissao.Property(x => x.DeUnidade).IsRequired();
            permissao.HasKey("VersaoId", nameof(PermissaoDaVersao.Codigo));
        });
    }
}
