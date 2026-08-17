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

        builder.Property(e => e.Nome).HasMaxLength(EtapaProcesso.NomeMaxLength).IsRequired();
        builder.Property(e => e.Carater).HasConversion<int>().IsRequired();

        builder.Ignore(e => e.TipoEtapaOrigemId);
        // Sem produção em nenhum ambiente: o vínculo nasce obrigatório desde a primeira
        // migration, sem coluna nullable transitória (issue #1071).
        builder.OwnsOne(e => e.TipoEtapa, tipo =>
        {
            tipo.Property(x => x.OrigemId)
                .HasColumnName("tipo_etapa_origem_id")
                .IsRequired()
                .HasComment("Id de origem do tipo de etapa em Configuração, sem FK cross-schema; congelado na definição.");
            tipo.Property(x => x.Codigo).HasColumnName("tipo_etapa_codigo").HasMaxLength(64).IsRequired();
            tipo.Property(x => x.Nome).HasColumnName("tipo_etapa_nome").HasMaxLength(200).IsRequired();
        });
        builder.Navigation(e => e.TipoEtapa).IsRequired();

        builder.Property(e => e.Peso).HasPrecision(18, 4);
        builder.Property(e => e.NotaMinima).HasPrecision(18, 4);

        // Ordem única por processo quando informada (colunas nulas não
        // colidem em unique index no Postgres).
        builder.HasIndex(e => new { e.ProcessoSeletivoId, e.Ordem }).IsUnique();
    }
}
