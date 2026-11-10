namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das desativações do perfil: o registro de estado, com no máximo uma aberta por perfil.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DesativacaoDoPerfilConfiguration : IEntityTypeConfiguration<DesativacaoDoPerfil>
{
    private const int IdentidadeMaxLength = 255;

    public void Configure(EntityTypeBuilder<DesativacaoDoPerfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("desativacoes_do_perfil", ConfigurarRestricoes);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.DesativadaEm).IsRequired();
        builder.Property(d => d.DesativadaPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DesativadaPorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DesativadaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(d => d.Motivo).HasMaxLength(MotivosDaVersao.MaxLength).IsRequired();
        builder.Property(d => d.ReativadaPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(d => d.ReativadaPorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(d => d.ReativadaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(d => d.MotivoDaReativacao).HasMaxLength(MotivosDaVersao.MaxLength);
        builder.Property(d => d.OrdemNaTrilha).IsRequired();

        builder.Ignore(d => d.Aberta);
        builder.Ignore(d => d.DesativadaPor);
        builder.Ignore(d => d.OrdinalLocal);

        builder.HasIndex(d => d.PerfilId).IsUnique().HasFilter("reativada_em IS NULL").HasDatabaseName("ux_desativacoes_do_perfil_aberta");
        builder.HasIndex(d => d.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_desativacoes_do_perfil_ordem");
    }

    // Ou nenhum campo da reativação existe, ou todos os obrigatórios existem; o nome de quem reativa é sempre opcional.
    private static void ConfigurarRestricoes(TableBuilder<DesativacaoDoPerfil> tabela) =>
        tabela.HasCheckConstraint(
            "ck_desativacoes_do_perfil_reativacao",
            "(reativada_em IS NULL AND reativada_por_emissor IS NULL AND reativada_por_sub IS NULL"
            + " AND reativada_por_nome IS NULL AND motivo_da_reativacao IS NULL)"
            + " OR (reativada_em IS NOT NULL AND reativada_por_emissor IS NOT NULL AND reativada_por_sub IS NOT NULL"
            + " AND motivo_da_reativacao IS NOT NULL)");
}
