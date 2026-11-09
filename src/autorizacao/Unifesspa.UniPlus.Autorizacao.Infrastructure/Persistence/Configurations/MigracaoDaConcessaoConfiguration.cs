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

/// <summary>Configuração EF Core da situação de cada concessão na janela de migração.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class MigracaoDaConcessaoConfiguration : IEntityTypeConfiguration<MigracaoDaConcessao>
{
    private const int SituacaoMaxLength = 40;
    private const int DispensaMaxLength = 80;

    private static readonly ValueConverter<SituacaoDaMigracao, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDaMigracaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<MigracaoDaConcessao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
        builder.ToTable("migracoes_da_concessao", ConfigurarRestricoes);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(m => m.ExigeProtecaoDeDados).IsRequired();
        builder.Property(m => m.Dispensa).HasMaxLength(DispensaMaxLength);
        builder.Property(m => m.AtualizadaEm).IsRequired();

        // A concessão é do mesmo perfil da janela: a chave composta impede a linha de apontar para a de outro perfil.
        builder.HasOne<Concessao>()
            .WithMany()
            .HasForeignKey(m => new { m.PerfilId, m.ConcessaoId })
            .HasPrincipalKey(c => new { c.PerfilId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.JanelaId, m.ConcessaoId }).IsUnique().HasDatabaseName("ux_migracoes_da_concessao_janela_concessao");
        builder.Property<uint>("Version").IsRowVersion();
    }

    private static void ConfigurarRestricoes(TableBuilder<MigracaoDaConcessao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_migracoes_da_concessao_situacao",
            $"situacao IN ('{SituacaoDaMigracaoCodigo.Retida}', '{SituacaoDaMigracaoCodigo.AguardandoProtecaoDeDados}', '{SituacaoDaMigracaoCodigo.Migrada}', '{SituacaoDaMigracaoCodigo.Recusada}', '{SituacaoDaMigracaoCodigo.NaoMigrada}', '{SituacaoDaMigracaoCodigo.ForaDaLista}')");
        tabela.HasCheckConstraint(
            "ck_migracoes_da_concessao_dispensa",
            $"dispensa IS NULL OR dispensa = '{MotivosDeDispensa.BeneficiarioJaTemProtecaoDeDados}'");
    }
}
