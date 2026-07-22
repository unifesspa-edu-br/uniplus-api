namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class EtapaProcessoConfiguration : IEntityTypeConfiguration<EtapaProcesso>
{
    public void Configure(EntityTypeBuilder<EtapaProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("etapas_processo");
        builder.HasKey(e => e.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Nome).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Carater).HasConversion<int>().IsRequired();
        builder.Property(e => e.Peso).HasPrecision(18, 4);
        builder.Property(e => e.NotaMinima).HasPrecision(18, 4);

        // Ordem única por processo quando informada (colunas nulas não
        // colidem em unique index no Postgres).
        builder.HasIndex(e => new { e.ProcessoSeletivoId, e.Ordem }).IsUnique();
    }
}
