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

        // O vínculo com a fase: o código é o que o cliente declara e o envelope congela; o
        // id é resolvido pela raiz. Sem FK por ora — a fase é substituída por inteiro a
        // cada gravação do cronograma, e uma FK restritiva recusaria a substituição.
        builder.Property(e => e.FaseCodigo)
            .HasColumnName("fase_codigo")
            .HasMaxLength(60)
            .HasComment("Código canônico da fase declarada pelo cliente; a raiz o resolve para fase_cronograma_id.");
        builder.Property(e => e.FaseCronogramaId)
            .HasColumnName("fase_cronograma_id")
            .HasComment("Fase do cronograma a que a etapa pertence, resolvida a partir de fase_codigo.");
        builder.HasIndex(e => e.FaseCronogramaId);

        // Produtos da etapa em cascata: a configuração em rascunho é substituída por
        // inteiro, e nada fora do agregado referencia o produto por FK.
        builder.HasMany(e => e.Produtos)
            .WithOne()
            .HasForeignKey(p => p.EtapaProcessoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(EtapaProcesso.Produtos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(e => e.Peso).HasPrecision(18, 4);
        builder.Property(e => e.NotaMinima).HasPrecision(18, 4);

        // Ordem única por processo quando informada (colunas nulas não
        // colidem em unique index no Postgres).
        builder.HasIndex(e => new { e.ProcessoSeletivoId, e.Ordem }).IsUnique();
    }
}
