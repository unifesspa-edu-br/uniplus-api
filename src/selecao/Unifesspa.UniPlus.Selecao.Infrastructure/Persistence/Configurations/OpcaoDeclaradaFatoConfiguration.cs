namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="OpcaoDeclaradaFato"/> (issue #1619) — entidade filha de
/// <c>ProcessoSeletivo</c>, <c>EntityBase</c> puro, substituída por fato pelo agregado.
/// </summary>
public sealed class OpcaoDeclaradaFatoConfiguration : IEntityTypeConfiguration<OpcaoDeclaradaFato>
{
    public void Configure(EntityTypeBuilder<OpcaoDeclaradaFato> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(o => o.ProcessoSeletivoId).HasComment("Identificador do processo seletivo ao qual pertence.");
        builder.Property(o => o.FatoCodigo).HasComment("Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(o => o.Codigo).HasComment("Código da opção de resposta.");
        builder.Property(o => o.Rotulo).HasComment("Texto apresentado à pessoa candidata.");
        builder.Property(o => o.Ordem).HasComment("Posição de apresentação ou avaliação no conjunto.");

        builder.ToTable("opcoes_declaradas_do_dado", t => t.HasComment("Opções de resposta declaradas pelo processo para um dado."));
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.FatoCodigo).HasColumnName("codigo_do_dado").HasMaxLength(OpcaoDeclaradaFato.FatoCodigoMaxLength).IsRequired();
        builder.Property(o => o.Codigo).HasMaxLength(OpcaoDeclaradaFato.CodigoMaxLength).IsRequired();
        builder.Property(o => o.Rotulo).HasMaxLength(OpcaoDeclaradaFato.RotuloMaxLength).IsRequired();
        builder.Property(o => o.Ordem).IsRequired();

        // Código único por fato no processo: invariante do agregado, também contra escrita
        // concorrente e caminho que não passe por ele.
        builder.HasIndex(o => new { o.ProcessoSeletivoId, o.FatoCodigo, o.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_opcoes_declaradas_do_dado_processo_dado_codigo");
    }
}
