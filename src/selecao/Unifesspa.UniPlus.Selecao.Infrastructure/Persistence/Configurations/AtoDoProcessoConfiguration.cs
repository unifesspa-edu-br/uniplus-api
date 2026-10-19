namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AtoDoProcessoConfiguration : IEntityTypeConfiguration<AtoDoProcesso>
{
    public void Configure(EntityTypeBuilder<AtoDoProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("atos_do_processo");
        builder.HasKey(a => a.Id);
        // O Id é o AtoId que a Seleção decidiu (e não um Guid gerado pelo banco).
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.TipoCodigo).HasMaxLength(AtoDoProcesso.TipoCodigoMaxLength).IsRequired();
        builder.Property(a => a.TipoNome).HasMaxLength(AtoDoProcesso.TipoNomeMaxLength).IsRequired();
        builder.Property(a => a.Numero).HasMaxLength(AtoDoProcesso.NumeroMaxLength);
        builder.Property(a => a.Assinante).HasMaxLength(AtoDoProcesso.AssinanteMaxLength).IsRequired();
        builder.Property(a => a.UnidadeAdministradora).HasMaxLength(AtoDoProcesso.UnidadeAdministradoraMaxLength).IsRequired();
        builder.Property(a => a.IdentificadorLegivelDoProcesso).HasMaxLength(AtoDoProcesso.IdentificadorLegivelMaxLength).IsRequired();
        builder.Property(a => a.MotivoDaCorrecao).HasMaxLength(AtoDoProcesso.MotivoDaCorrecaoMaxLength);
        builder.Property(a => a.PoliticaDeDivulgacaoConfirmadaPorSub).HasMaxLength(AtoDoProcesso.SubMaxLength);
        builder.Property(a => a.ItemDoEdital).HasMaxLength(IdentidadeDoItemDoEdital.ChaveMaxLength);
        builder.Property(a => a.Situacao).HasConversion<int>().IsRequired();

        // Vínculo por FK ao processo, sem navegação inversa: o registro não é filho do agregado. O
        // processo é soft-deletable e nunca emite DELETE, então o vínculo é Restrict e não carrega
        // uma remoção em cascata que jamais dispararia.
        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(a => a.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Restrict);

        // O principal é um documento a publicar confirmado, imutável e nunca removido pela rotina de
        // pendentes; a FK impede que o registro aponte para um documento que não existe. Sem unicidade:
        // a retificação do edital pode reaproveitar o documento do ato anterior.
        builder.HasOne<DocumentoAPublicar>()
            .WithMany()
            .HasForeignKey(a => a.DocumentoPrincipalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sem FK: o ato corrigido pode ser o que criou uma versão ou outro registro, e ele é do
        // Publicações (ADR-0061). Único: a cadeia de correção é linear (ADR-0103), e o Publicações só
        // enxerga a correção depois de registrá-la — duas correções pendentes do mesmo ato só são
        // barradas aqui.
        builder.HasIndex(a => a.AtoCorrigidoId)
            .IsUnique()
            .HasFilter("ato_corrigido_id IS NOT NULL")
            .HasDatabaseName("ux_atos_do_processo_ato_corrigido");

        // Cada item que o edital declara tem uma única publicação original: a que não corrige ato
        // algum. O item é identificado pelo lugar, o tipo de ato e o papel, que valem em qualquer versão
        // do edital; o Id do produto muda entre versões e não serviria à unicidade. A correção de um item
        // publicado leva o mesmo item e aponta o ato corrigido, e por isso fica fora do índice. Duas
        // publicações simultâneas do mesmo item só são barradas aqui.
        builder.HasIndex(a => new { a.ProcessoSeletivoId, a.ItemDoEdital })
            .IsUnique()
            .HasFilter("item_do_edital IS NOT NULL AND ato_corrigido_id IS NULL")
            .HasDatabaseName("ux_atos_do_processo_item_original");

        // A consulta administrativa filtra por processo e entrega do mais recente para o mais
        // antigo; as colunas descendentes evitam o passo de ordenação.
        builder.HasIndex(a => new { a.ProcessoSeletivoId, a.CreatedAt, a.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_atos_do_processo_processo_recentes");

        builder.HasMany(a => a.Anexos)
            .WithOne()
            .HasForeignKey(x => x.AtoDoProcessoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Anexos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
