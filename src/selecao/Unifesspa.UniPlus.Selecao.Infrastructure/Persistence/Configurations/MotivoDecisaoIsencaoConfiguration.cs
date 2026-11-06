namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core do catálogo de motivos de decisão de isenção
/// (UNI-REQ-0120).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class MotivoDecisaoIsencaoConfiguration : IEntityTypeConfiguration<MotivoDecisaoIsencao>
{
    private const int CodigoMaxLength = 50;
    private const int DescricaoMaxLength = 500;
    private const int FundamentoMaxLength = 32;
    private const int ResultadoPermitidoMaxLength = 16;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<MotivoDecisaoIsencao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("motivos_decisao_isencao");
        builder.HasKey(m => m.Id);

        MapearColunas(builder);
        MapearConcorrenciaEAuditoria(builder);
        MapearIndices(builder);
    }

    private static void MapearColunas(EntityTypeBuilder<MotivoDecisaoIsencao> builder)
    {
        builder.Property(m => m.Codigo)
            .HasConversion(
                codigo => codigo.Valor,
                valor => CodigoMotivoDecisao.Reidratar(valor))
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(m => m.Descricao)
            .HasMaxLength(DescricaoMaxLength)
            .IsRequired();

        // Texto, e não inteiro: auditoria e consulta manual leem a coluna sem conhecer a
        // ordem de declaração do enum.
        builder.Property(m => m.Fundamento)
            .HasConversion<string>()
            .HasMaxLength(FundamentoMaxLength)
            .IsRequired();

        builder.Property(m => m.ResultadoPermitido)
            .HasConversion<string>()
            .HasMaxLength(ResultadoPermitidoMaxLength)
            .IsRequired();

        builder.Property(m => m.Ativo).IsRequired();
    }

    private static void MapearConcorrenciaEAuditoria(EntityTypeBuilder<MotivoDecisaoIsencao> builder)
    {
        // xmin do Postgres como token de concorrência otimista (ADR-0119), sem coluna nem
        // migration própria. Reativar e desativar são check-then-act: sem o token, a segunda de
        // duas requisições concorrentes relataria uma transição que não aconteceu e
        // sobrescreveria a auditoria da primeira.
        builder.Property<uint>("Version").IsRowVersion();

        builder.Property(m => m.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(AuditUserMaxLength);
    }

    private static void MapearIndices(EntityTypeBuilder<MotivoDecisaoIsencao> builder)
    {
        // Unicidade total, sem filtro por situação: o código é citado nas decisões já proferidas, e
        // liberá-lo ao desativar faria dois motivos responderem pelo mesmo rótulo no histórico. O
        // CodigoExisteAsync do handler devolve 409 antes do INSERT; a corrida real é barrada aqui.
        builder.HasIndex(m => m.Codigo)
            .IsUnique()
            .HasDatabaseName("ux_motivos_decisao_isencao_codigo");

        // Leitura do catálogo: quem monta uma publicação pede os motivos ativos de um fundamento.
        builder.HasIndex(m => new { m.Fundamento, m.Ativo })
            .HasDatabaseName("ix_motivos_decisao_isencao_fundamento_ativo");
    }
}
