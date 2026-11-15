namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class DeficienciaAtendidaConfiguration : IEntityTypeConfiguration<DeficienciaAtendida>
{
    public void Configure(EntityTypeBuilder<DeficienciaAtendida> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("deficiencias_atendidas");
        builder.HasKey(t => t.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TipoDeficienciaOrigemId).IsRequired();

        // Teto do código no cadastro de origem (CodigoTipoDeficiencia). É por ele que
        // a regra legal referencia o tipo — o nome fica como rótulo de exibição.
        builder.Property(t => t.TipoDeficienciaCodigo).HasMaxLength(50).IsRequired();

        builder.Property(t => t.TipoDeficienciaNome).HasMaxLength(300).IsRequired();

        builder.HasIndex(t => new { t.AtendimentoEspecializadoId, t.TipoDeficienciaOrigemId }).IsUnique()
            .HasDatabaseName("ux_deficiencias_atendidas_por_atendimento_origem");
    }
}
