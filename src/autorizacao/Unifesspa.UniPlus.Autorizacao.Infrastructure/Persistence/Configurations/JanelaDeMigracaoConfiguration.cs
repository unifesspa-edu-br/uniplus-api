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

/// <summary>Configuração EF Core da janela de migração, raiz do agregado de lotes, linhas e decisões.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class JanelaDeMigracaoConfiguration : IEntityTypeConfiguration<JanelaDeMigracao>
{
    private const int IdentidadeMaxLength = 255;
    private const int FormaMaxLength = 20;

    private static readonly ValueConverter<FormaDoEncerramento, string> FormaConverter = new(
        forma => forma.ToCodigo(),
        codigo => FormaDoEncerramentoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<JanelaDeMigracao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
        builder.ToTable("janelas_de_migracao", ConfigurarRestricoes);
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        ConfigurarColunas(builder);
        ConfigurarRelacionamentos(builder);
        ConfigurarFilhos(builder);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }

    private static void ConfigurarRestricoes(TableBuilder<JanelaDeMigracao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_janelas_de_migracao_encerramento",
            "(encerrada_em IS NULL) = (forma_do_encerramento IS NULL)");
        tabela.HasCheckConstraint(
            "ck_janelas_de_migracao_forma",
            $"forma_do_encerramento IS NULL OR forma_do_encerramento IN ('{FormaDoEncerramentoCodigo.Concluida}', '{FormaDoEncerramentoCodigo.Prazo}', '{FormaDoEncerramentoCodigo.Manual}')");
        tabela.HasCheckConstraint(
            "ck_janelas_de_migracao_manual",
            $"COALESCE(forma_do_encerramento = '{FormaDoEncerramentoCodigo.Manual}', false) = (motivo_do_encerramento IS NOT NULL AND encerrada_por_sub IS NOT NULL)");
        tabela.HasCheckConstraint(
            "ck_janelas_de_migracao_prazo",
            "prazo = aberta_em + interval '72 hours'");
    }

    private static void ConfigurarColunas(EntityTypeBuilder<JanelaDeMigracao> builder)
    {
        builder.Property(j => j.NumeroDaVersaoNova).IsRequired();
        builder.Property(j => j.AbertaEm).IsRequired();
        builder.Property(j => j.OrdemDeRegistro).UseIdentityAlwaysColumn().ValueGeneratedOnAdd();
        builder.HasIndex(j => j.OrdemDeRegistro).IsUnique().HasDatabaseName("ux_janelas_de_migracao_ordem_de_registro");
        builder.Property(j => j.Prazo).IsRequired();
        builder.Property(j => j.Forma).HasColumnName("forma_do_encerramento").HasConversion(FormaConverter).HasMaxLength(FormaMaxLength);
        builder.Property(j => j.MotivoDoEncerramento).HasMaxLength(JanelaDeMigracao.MotivoMaxLength);
        builder.Property(j => j.EncerradaPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(j => j.EncerradaPorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(j => j.EncerradaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(j => j.Aviso48hEm).HasColumnName("aviso_48h_em");
        builder.Property(j => j.Aviso24hEm).HasColumnName("aviso_24h_em");
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<JanelaDeMigracao> builder)
    {
        builder.HasOne<Perfil>().WithMany().HasForeignKey(j => j.PerfilId).OnDelete(DeleteBehavior.Restrict);

        // As versões são do mesmo perfil: a chave composta impede a janela de apontar para a versão de outro.
        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(j => new { j.PerfilId, j.VersaoNovaId })
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(j => new { j.PerfilId, j.VersaoAlvoId })
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Alvo das chaves estrangeiras compostas dos lotes, das linhas e das decisões.
        builder.HasAlternateKey(j => new { j.Id, j.PerfilId });

        // Um perfil tem no máximo uma janela não encerrada.
        builder.HasIndex(j => j.PerfilId)
            .IsUnique()
            .HasFilter("encerrada_em IS NULL")
            .HasDatabaseName("ux_janelas_de_migracao_aberta");
    }

    private static void ConfigurarFilhos(EntityTypeBuilder<JanelaDeMigracao> builder)
    {
        builder.HasMany(j => j.Lotes).WithOne().HasForeignKey(l => new { l.JanelaId, l.PerfilId })
            .HasPrincipalKey(j => new { j.Id, j.PerfilId }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(j => j.Lotes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(j => j.Migracoes).WithOne().HasForeignKey(m => new { m.JanelaId, m.PerfilId })
            .HasPrincipalKey(j => new { j.Id, j.PerfilId }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(j => j.Migracoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(j => j.Decisoes).WithOne().HasForeignKey(d => new { d.JanelaId, d.PerfilId })
            .HasPrincipalKey(j => new { j.Id, j.PerfilId }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(j => j.Decisoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
