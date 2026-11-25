namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core da revisão das concessões, raiz do agregado de envios, concessões e decisões.</summary>
internal sealed class RevisaoDasConcessoesConfiguration : IEntityTypeConfiguration<RevisaoDasConcessoesDoPerfil>
{
    private const int IdentidadeMaxLength = 255;
    private const int FormaMaxLength = 20;

    public void Configure(EntityTypeBuilder<RevisaoDasConcessoesDoPerfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
        builder.ToTable("revisoes_das_concessoes", ConfigurarRestricoes);
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        ConfigurarColunas(builder);
        ConfigurarRelacionamentos(builder);
        ConfigurarFilhos(builder);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }

    private static void ConfigurarRestricoes(TableBuilder<RevisaoDasConcessoesDoPerfil> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_revisoes_das_concessoes_encerramento",
            "(encerrada_em IS NULL) = (forma_do_encerramento IS NULL)");
        tabela.HasCheckConstraint(
            "ck_revisoes_das_concessoes_forma",
            $"forma_do_encerramento IS NULL OR forma_do_encerramento IN ({SqlDosCodigosDeContrato.Lista<FormaDoEncerramento>()})");
        tabela.HasCheckConstraint(
            "ck_revisoes_das_concessoes_manual",
            $"COALESCE(forma_do_encerramento = {SqlDosCodigosDeContrato.Literal(FormaDoEncerramento.Manual)}, false) = (motivo_do_encerramento IS NOT NULL AND encerrada_por_conta_id IS NOT NULL)");
        tabela.HasCheckConstraint(
            "ck_revisoes_das_concessoes_termino",
            "termina_em = aberta_em + interval '72 hours'");
    }

    private static void ConfigurarColunas(EntityTypeBuilder<RevisaoDasConcessoesDoPerfil> builder)
    {
        builder.Property(j => j.NumeroDaVersaoNova).IsRequired();
        builder.Property(j => j.AbertaEm).IsRequired();
        builder.Property(j => j.OrdemDeRegistro).UseIdentityAlwaysColumn().ValueGeneratedOnAdd();
        builder.HasIndex(j => j.OrdemDeRegistro).IsUnique().HasDatabaseName("ux_revisoes_das_concessoes_ordem_de_registro");
        builder.Property(j => j.TerminaEm).IsRequired();
        builder.Property(j => j.Forma).HasColumnName("forma_do_encerramento").HasMaxLength(FormaMaxLength);
        builder.Property(j => j.MotivoDoEncerramento).HasMaxLength(MotivoInformado.MaximoDeCaracteres);
        builder.Property(j => j.EncerradaPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(j => j.EncerradaPorContaId).HasMaxLength(IdentidadeMaxLength);
        builder.Property(j => j.EncerradaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(j => j.Aviso48hEm).HasColumnName("aviso_48h_em");
        builder.Property(j => j.Aviso24hEm).HasColumnName("aviso_24h_em");
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<RevisaoDasConcessoesDoPerfil> builder)
    {
        builder.HasOne<Perfil>().WithMany().HasForeignKey(j => j.PerfilId).OnDelete(DeleteBehavior.Restrict);

        // As versões são do mesmo perfil: a chave composta impede a revisão de apontar para a versão de outro.
        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(j => new { j.PerfilId, j.VersaoNovaId })
            .HasConstraintName("fk_revisoes_das_concessoes_versao_do_perfil_nova")
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(j => new { j.PerfilId, j.VersaoAlvoId })
            .HasConstraintName("fk_revisoes_das_concessoes_versao_do_perfil_alvo")
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Alvo das chaves estrangeiras compostas dos envios, das concessões e das decisões.
        builder.HasAlternateKey(j => new { j.Id, j.PerfilId });

        // Um perfil tem no máximo uma revisão não encerrada.
        builder.HasIndex(j => j.PerfilId)
            .IsUnique()
            .HasFilter("encerrada_em IS NULL")
            .HasDatabaseName("ux_revisoes_das_concessoes_aberta");
    }

    private static void ConfigurarFilhos(EntityTypeBuilder<RevisaoDasConcessoesDoPerfil> builder)
    {
        builder.HasMany(j => j.Envios).WithOne().HasForeignKey(l => new { l.RevisaoId, l.PerfilId })
            .HasConstraintName("fk_envios_de_decisoes_revisao_das_concessoes")
            .HasPrincipalKey(j => new { j.Id, j.PerfilId }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(j => j.Envios).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(j => j.Migracoes).WithOne().HasForeignKey(m => new { m.RevisaoId, m.PerfilId })
            .HasConstraintName("fk_migracoes_da_concessao_revisao_das_concessoes")
            .HasPrincipalKey(j => new { j.Id, j.PerfilId }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(j => j.Migracoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(j => j.Decisoes).WithOne().HasForeignKey(d => new { d.RevisaoId, d.PerfilId })
            .HasConstraintName("fk_decisoes_da_migracao_revisao_das_concessoes")
            .HasPrincipalKey(j => new { j.Id, j.PerfilId }).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(j => j.Decisoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
