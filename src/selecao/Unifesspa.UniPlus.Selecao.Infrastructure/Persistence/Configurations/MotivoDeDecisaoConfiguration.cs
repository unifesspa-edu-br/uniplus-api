namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Configuração EF Core do cadastro único de motivos de decisão (UNI-REQ-0283).</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class MotivoDeDecisaoConfiguration : IEntityTypeConfiguration<MotivoDeDecisao>
{
    internal const int CodigoMaxLength = 50;
    internal const int EnumMaxLength = 32;
    internal const int SubclassificacaoMaxLength = 64;
    internal const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<MotivoDeDecisao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("motivos_de_decisao");
        builder.HasKey(m => m.Id);

        MapearColunas(builder);
        MapearConcorrenciaEAuditoria(builder);
        MapearIndices(builder);
    }

    private static void MapearColunas(EntityTypeBuilder<MotivoDeDecisao> builder)
    {
        // Texto, e não inteiro: auditoria e consulta manual leem a coluna sem conhecer a ordem de
        // declaração do enum.
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(EnumMaxLength).IsRequired();
        builder.Property(m => m.Codigo)
            .HasConversion(codigo => codigo.Valor, valor => CodigoMotivoDecisao.Reidratar(valor))
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();
        builder.Property(m => m.Descricao).HasMaxLength(ValidacaoDoMotivo.DescricaoMaxLength).IsRequired();
        builder.Property(m => m.Subclassificacao).HasMaxLength(SubclassificacaoMaxLength);
        builder.Property(m => m.Sentido).HasConversion<string>().HasMaxLength(EnumMaxLength).IsRequired();
        builder.Property(m => m.Ativo).IsRequired();
    }

    private static void MapearConcorrenciaEAuditoria(EntityTypeBuilder<MotivoDeDecisao> builder)
    {
        // xmin do Postgres como token de concorrência otimista (ADR-0119): reativar e desativar são
        // check-then-act, e sem o token a segunda de duas requisições concorrentes relataria uma
        // transição que não aconteceu.
        builder.Property<uint>("Version").IsRowVersion();

        builder.Property(m => m.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(AuditUserMaxLength);
    }

    private static void MapearIndices(EntityTypeBuilder<MotivoDeDecisao> builder)
    {
        // Único por tipo e sem filtro por situação: o código é citado nas decisões já proferidas, e
        // liberá-lo ao desativar faria dois motivos responderem pelo mesmo rótulo no histórico. O
        // mesmo código em outro tipo é outro motivo.
        builder.HasIndex(m => new { m.Tipo, m.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_motivos_de_decisao_tipo_codigo");

        // Leitura do cadastro: quem monta uma publicação pede os motivos ativos de um tipo e subclassificação.
        builder.HasIndex(m => new { m.Tipo, m.Subclassificacao, m.Ativo })
            .HasDatabaseName("ix_motivos_de_decisao_tipo_subclassificacao_ativo");
    }
}
