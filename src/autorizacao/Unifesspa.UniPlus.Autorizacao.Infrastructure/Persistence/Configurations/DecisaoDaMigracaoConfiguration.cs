namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core da trilha de decisões da revisão das concessões, registro só acrescentado.</summary>
internal sealed class DecisaoDaMigracaoConfiguration : IEntityTypeConfiguration<DecisaoDaMigracao>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;

    private const string Gestao = EtapaDaMigracaoCodigo.GestaoDeAcessos;
    private const string Protecao = EtapaDaMigracaoCodigo.ProtecaoDeDados;

    private static readonly ValueConverter<EtapaDaMigracao, string> EtapaConverter = new(
        etapa => etapa.ToCodigo(),
        codigo => EtapaDaMigracaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<ResultadoDaMigracao, string> ResultadoConverter = new(
        resultado => resultado.ToCodigo(),
        codigo => ResultadoDaMigracaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<AtorDaMigracao, string> AtorConverter = new(
        ator => ator.ToCodigo(),
        codigo => AtorDaMigracaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<DecisaoDaMigracao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
        builder.ToTable("decisoes_da_migracao", ConfigurarRestricoes);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        ConfigurarColunas(builder);
        ConfigurarRelacionamentos(builder);
        builder.Ignore(d => d.Decisor);
    }

    private static void ConfigurarColunas(EntityTypeBuilder<DecisaoDaMigracao> builder)
    {
        builder.Property(d => d.EnvioDeOrigemEtapa).HasConversion(EtapaConverter).HasMaxLength(CodigoMaxLength);
        builder.Property(d => d.EnvioEtapa).HasConversion(EtapaConverter).HasMaxLength(CodigoMaxLength);
        builder.Property(d => d.Etapa).HasConversion(EtapaConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Resultado).HasColumnName("decisao").HasConversion(ResultadoConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Ator).HasConversion(AtorConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.DecisorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(d => d.DecisorContaId).HasMaxLength(IdentidadeMaxLength);
        builder.Property(d => d.DecisorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(d => d.ComposicaoAvaliada).IsRequired();
        builder.Property(d => d.AcrescimoAvaliado).IsRequired();
        builder.Property(d => d.TerminaEm).IsRequired();
        builder.Property(d => d.Instante).IsRequired();
        builder.Property(d => d.OrdemDeRegistro).UseIdentityAlwaysColumn().ValueGeneratedOnAdd();
        builder.HasIndex(d => d.OrdemDeRegistro).IsUnique().HasDatabaseName("ux_decisoes_da_migracao_ordem_de_registro");
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<DecisaoDaMigracao> builder)
    {
        builder.HasOne<Concessao>()
            .WithMany()
            .HasForeignKey(d => new { d.PerfilId, d.ConcessaoId })
            .HasPrincipalKey(c => new { c.PerfilId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(d => new { d.PerfilId, d.VersaoAvaliadaId })
            .HasConstraintName("fk_decisoes_da_migracao_versao_do_perfil_avaliada")
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Chave composta com o segundo componente nulo: o banco só confere o par quando os dois estão preenchidos.
        builder.HasOne<EnvioDeDecisoesDaRevisao>()
            .WithMany()
            .HasForeignKey(d => new { d.RevisaoId, d.EnvioDeOrigemId, d.EnvioDeOrigemEtapa })
            .HasConstraintName("fk_decisoes_da_migracao_envio_de_origem")
            .HasPrincipalKey(l => new { l.RevisaoId, l.Id, l.Etapa })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.RevisaoId, d.EnvioDeOrigemId, d.EnvioDeOrigemEtapa })
            .HasDatabaseName("ix_decisoes_da_migracao_revisao_envio_de_origem_e_etapa");

        builder.HasOne<EnvioDeDecisoesDaRevisao>()
            .WithMany()
            .HasForeignKey(d => new { d.RevisaoId, d.EnvioId, d.EnvioEtapa })
            .HasConstraintName("fk_decisoes_da_migracao_envio")
            .HasPrincipalKey(l => new { l.RevisaoId, l.Id, l.Etapa })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurarRestricoes(TableBuilder<DecisaoDaMigracao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_decisoes_da_migracao_envio_de_origem",
            $"(envio_de_origem_id IS NULL) = (envio_de_origem_etapa IS NULL) AND (envio_de_origem_etapa IS NULL OR envio_de_origem_etapa = '{Gestao}')");
        tabela.HasCheckConstraint(
            "ck_decisoes_da_migracao_envio",
            $"(envio_id IS NULL) = (envio_etapa IS NULL) AND (envio_etapa IS NULL OR envio_etapa IN ('{Gestao}', '{Protecao}'))");
        tabela.HasCheckConstraint("ck_decisoes_da_migracao_ramo", $"({RamoHumano()}) OR ({RamoDoSistema()})");
    }

    private static string RamoHumano() =>
        $"ator = '{AtorDaMigracaoCodigo.Humano}'"
        + $" AND etapa IN ('{Gestao}', '{Protecao}')"
        + $" AND decisao IN ('{ResultadoDaMigracaoCodigo.Migrar}', '{ResultadoDaMigracaoCodigo.Recusar}')"
        + " AND decisor_emissor IS NOT NULL AND decisor_conta_id IS NOT NULL AND decisor_tem_concessao IS NOT NULL"
        + " AND envio_de_origem_id IS NOT NULL"
        + " AND (envio_etapa IS NULL OR envio_etapa = etapa)"
        + $" AND (etapa <> '{Protecao}' OR envio_id IS NOT NULL)"
        + $" AND (etapa <> '{Gestao}' OR envio_id IS NULL OR envio_id = envio_de_origem_id)";

    private static string RamoDoSistema() =>
        $"ator = '{AtorDaMigracaoCodigo.Sistema}'"
        + " AND decisor_emissor IS NULL AND decisor_conta_id IS NULL AND decisor_nome IS NULL AND decisor_tem_concessao IS NULL"
        + " AND envio_id IS NULL"
        + $" AND ((etapa = '{Gestao}' AND decisao = '{ResultadoDaMigracaoCodigo.Reter}' AND envio_de_origem_id IS NOT NULL)"
        + $" OR (etapa = '{EtapaDaMigracaoCodigo.Sistema}' AND decisao IN ('{ResultadoDaMigracaoCodigo.NaoMigrada}', '{ResultadoDaMigracaoCodigo.ForaDaLista}')))";
}
