namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class CondicaoOfertadaConfiguration : IEntityTypeConfiguration<CondicaoOfertada>
{
    public void Configure(EntityTypeBuilder<CondicaoOfertada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("condicoes_ofertadas");
        builder.HasKey(c => c.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CondicaoOrigemId).IsRequired();
        builder.Property(c => c.CondicaoCodigo).HasMaxLength(50).IsRequired();
        builder.Property(c => c.CondicaoNome).HasMaxLength(300).IsRequired();

        builder.HasIndex(c => new { c.AtendimentoEspecializadoId, c.CondicaoOrigemId }).IsUnique()
            .HasDatabaseName("ux_condicoes_ofertadas_por_atendimento_origem");
    }
}
