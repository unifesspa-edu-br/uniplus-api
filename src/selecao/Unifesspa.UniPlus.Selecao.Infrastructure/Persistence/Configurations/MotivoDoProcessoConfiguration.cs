namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core dos motivos escolhidos pelo processo (UNI-REQ-0285): cópia por valor, sem
/// chave estrangeira para o cadastro (ADR-0061), para que a linha viva possa mudar sem alcançar o processo.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class MotivoDoProcessoConfiguration : IEntityTypeConfiguration<MotivoDoProcesso>
{
    public void Configure(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("motivos_do_processo");
        builder.HasKey(m => m.Id);

        MapearCopia(builder);
        MapearEscolha(builder);

        // O mesmo motivo do cadastro entra uma vez por processo; a corrida de duas inclusões é barrada aqui.
        builder.HasIndex(m => new { m.ProcessoSeletivoId, m.MotivoDeDecisaoOrigemId })
            .IsUnique()
            .HasDatabaseName("ux_motivos_do_processo_processo_origem");
    }

    private static void MapearCopia(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(MotivoDeDecisaoConfiguration.EnumMaxLength).IsRequired();
        builder.Property(m => m.Codigo)
            .HasConversion(codigo => codigo.Valor, valor => CodigoMotivoDecisao.Reidratar(valor))
            .HasMaxLength(MotivoDeDecisaoConfiguration.CodigoMaxLength)
            .IsRequired();
        builder.Property(m => m.Descricao).HasMaxLength(ValidacaoDoMotivo.DescricaoMaxLength).IsRequired();
        builder.Property(m => m.Subclassificacao).HasMaxLength(MotivoDeDecisaoConfiguration.SubclassificacaoMaxLength);
        builder.Property(m => m.Sentido).HasConversion<string>().HasMaxLength(MotivoDeDecisaoConfiguration.EnumMaxLength).IsRequired();
    }

    private static void MapearEscolha(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        builder.Property(m => m.ProcessoSeletivoId).IsRequired();
        builder.Property(m => m.MotivoDeDecisaoOrigemId).IsRequired();
        builder.Property(m => m.Congelado).IsRequired();

        // Justificativa interna da inclusão depois da publicação: não vai ao candidato.
        builder.Property(m => m.JustificativaDaInclusao).HasMaxLength(EscolhaDosMotivos.JustificativaMaxLength);
        builder.Property<uint>("Version").IsRowVersion();
        builder.Property(m => m.CreatedBy).HasMaxLength(MotivoDeDecisaoConfiguration.AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(MotivoDeDecisaoConfiguration.AuditUserMaxLength);
    }
}
