namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        builder.ToTable("motivos_decisao_isencao");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Codigo)
            .HasConversion(
                codigo => codigo.Valor,
                valor => CodigoMotivoDecisao.Reidratar(valor))
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(m => m.Descricao)
            .HasMaxLength(DescricaoMaxLength)
            .IsRequired();

        // Enum como texto, e não como inteiro: a coluna é lida por auditoria e
        // por consulta manual, e o número exigiria conhecer a ordem de
        // declaração no C# para saber o que a linha diz.
        builder.Property(m => m.Fundamento)
            .HasConversion<string>()
            .HasMaxLength(FundamentoMaxLength)
            .IsRequired();

        builder.Property(m => m.ResultadoPermitido)
            .HasConversion<string>()
            .HasMaxLength(ResultadoPermitidoMaxLength)
            .IsRequired();

        builder.Property(m => m.Ativo).IsRequired();

        builder.Property(m => m.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Unicidade total, sem filtro por situação: o código é citado nas
        // decisões já proferidas, e liberá-lo ao desativar o motivo faria dois
        // motivos diferentes responderem pelo mesmo rótulo na leitura do
        // histórico. O CodigoExisteAsync do handler é check-then-act não
        // atômico e serve para devolver 409 antes do INSERT; a corrida real é
        // barrada aqui.
        builder.HasIndex(m => m.Codigo)
            .IsUnique()
            .HasDatabaseName("ux_motivos_decisao_isencao_codigo");

        // Caminho de leitura do catálogo: quem monta uma publicação pede os
        // motivos ativos de um fundamento.
        builder.HasIndex(m => new { m.Fundamento, m.Ativo })
            .HasDatabaseName("ix_motivos_decisao_isencao_fundamento_ativo");
    }
}
