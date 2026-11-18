namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core dos envios de decisões da revisão das concessões, registro só acrescentado.</summary>
internal sealed class EnvioDeDecisoesDaRevisaoConfiguration : IEntityTypeConfiguration<EnvioDeDecisoesDaRevisao>
{
    private const int IdentidadeMaxLength = 255;
    private const int EtapaMaxLength = 40;

    private static readonly ValueConverter<EtapaDaMigracao, string> EtapaConverter = new(
        etapa => etapa.ToCodigo(),
        codigo => EtapaDaMigracaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<EnvioDeDecisoesDaRevisao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
        builder.ToTable("envios_de_decisoes", tabela =>
            tabela.HasCheckConstraint(
                "ck_envios_de_decisoes_etapa",
                $"etapa IN ('{EtapaDaMigracaoCodigo.GestaoDeAcessos}', '{EtapaDaMigracaoCodigo.ProtecaoDeDados}')"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Etapa).HasConversion(EtapaConverter).HasMaxLength(EtapaMaxLength).IsRequired();
        builder.Property(l => l.EnviadoPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.EnviadoPorContaId).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.EnviadoPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(l => l.EnviadoPorTemConcessao).IsRequired();
        builder.Property(l => l.ConfirmouExclusao).IsRequired();
        builder.Property(l => l.Quantidade).IsRequired();
        builder.Property(l => l.Instante).IsRequired();

        builder.HasAlternateKey(l => new { l.RevisaoId, l.Id, l.Etapa });

        // A gestão envia as decisões uma vez por revisão; a proteção de dados pode enviá-las mais de uma vez.
        builder.HasIndex(l => l.RevisaoId)
            .IsUnique()
            .HasFilter($"etapa = '{EtapaDaMigracaoCodigo.GestaoDeAcessos}'")
            .HasDatabaseName("ux_envios_de_decisoes_gestao");
    }
}
