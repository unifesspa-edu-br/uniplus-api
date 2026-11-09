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

/// <summary>Configuração EF Core dos lotes da janela de migração, registro só acrescentado.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class LoteDaMigracaoConfiguration : IEntityTypeConfiguration<LoteDaMigracao>
{
    private const int IdentidadeMaxLength = 255;
    private const int EtapaMaxLength = 40;

    private static readonly ValueConverter<EtapaDaMigracao, string> EtapaConverter = new(
        etapa => etapa.ToCodigo(),
        codigo => EtapaDaMigracaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<LoteDaMigracao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
        builder.ToTable("lotes_da_migracao", tabela =>
            tabela.HasCheckConstraint(
                "ck_lotes_da_migracao_etapa",
                $"etapa IN ('{EtapaDaMigracaoCodigo.GestaoDeAcessos}', '{EtapaDaMigracaoCodigo.ProtecaoDeDados}')"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Etapa).HasConversion(EtapaConverter).HasMaxLength(EtapaMaxLength).IsRequired();
        builder.Property(l => l.EnviadoPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.EnviadoPorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.EnviadoPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(l => l.EnviadoPorTemConcessao).IsRequired();
        builder.Property(l => l.ConfirmouExclusao).IsRequired();
        builder.Property(l => l.Quantidade).IsRequired();
        builder.Property(l => l.Instante).IsRequired();

        // Alvo das chaves estrangeiras compostas da trilha: o lote de origem e o envio.
        builder.HasAlternateKey(l => new { l.JanelaId, l.Id, l.Etapa });

        // O lote da gestão é um só por janela; a proteção de dados envia quantas remessas precisar.
        builder.HasIndex(l => l.JanelaId)
            .IsUnique()
            .HasFilter($"etapa = '{EtapaDaMigracaoCodigo.GestaoDeAcessos}'")
            .HasDatabaseName("ux_lotes_da_migracao_gestao");
    }
}
