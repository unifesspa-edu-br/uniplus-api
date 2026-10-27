namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Mapeamento de <see cref="ItemDeAvaliacao"/> — filha de <see cref="EtapaProcesso"/>.</summary>
public sealed class ItemDeAvaliacaoConfiguration : IEntityTypeConfiguration<ItemDeAvaliacao>
{
    public void Configure(EntityTypeBuilder<ItemDeAvaliacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Só a positividade fica no banco: que a soma dos itens feche a nota máxima da etapa e
        // que a pontuação caiba no passo dependem de outra linha e do rascunho, e são regra do
        // domínio e do gate de publicação.
        builder.ToTable("itens_de_avaliacao_da_etapa", t =>
        {
            t.HasComment("Itens de avaliação (critérios ou blocos) da etapa; o id é a identidade a que lançamento e recurso apontam.");
            t.HasCheckConstraint("ck_itens_de_avaliacao_da_etapa_pontuacao_positiva", "pontuacao_maxima > 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Rotulo).HasMaxLength(ItemDeAvaliacao.RotuloMaxLength).IsRequired();
        builder.Property(i => i.PontuacaoMaxima).HasPrecision(18, 4)
            .HasComment("O quanto o item vale da nota máxima da etapa.");

        builder.HasIndex(i => i.EtapaProcessoId);
    }
}
