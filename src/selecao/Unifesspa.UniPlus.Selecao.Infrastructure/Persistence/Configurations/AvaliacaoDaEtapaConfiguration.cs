namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core das avaliações da etapa: uma linha por inscrição e oferta concorrida de cada
/// lote. A nota é <c>numeric</c> sem precisão, de propósito: com escala o banco arredondaria em
/// silêncio, e o arredondamento é regra própria da classificação. Sem cifra, sem nome, sem CPF.
/// </summary>
internal sealed class AvaliacaoDaEtapaConfiguration : IEntityTypeConfiguration<AvaliacaoDaEtapa>
{
    public void Configure(EntityTypeBuilder<AvaliacaoDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("avaliacoes_da_etapa", t =>
        {
            t.HasComment("A nota da etapa de uma inscrição numa oferta de curso concorrida (1ª ou 2ª opção), apurada pelos pesos do grupo de área do curso quando o lote foi importado. Os registros não são alterados ou excluídos; a avaliação vale com o lote. A nota por área está nos lançamentos do mesmo lote.");
            t.HasCheckConstraint(
                "ck_avaliacoes_da_etapa_situacao",
                $"situacao IN ({SqlDosCodigosDeContrato.Lista<SituacaoDaAvaliacao>()})");
            t.HasCheckConstraint(
                "ck_avaliacoes_da_etapa_nota",
                $"(situacao = {SqlDosCodigosDeContrato.Literal(SituacaoDaAvaliacao.Apurada)}) = (nota IS NOT NULL) AND (nota IS NULL OR nota BETWEEN 0 AND 1000)");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever().HasComment("Identificador da avaliação da inscrição na etapa.");

        builder.Property(a => a.LoteId).HasColumnName("importacao_id").IsRequired().HasComment("Identificador da importação que originou a avaliação.");

        builder.Property(a => a.EtapaProcessoId)
            .IsRequired()
            .HasComment("A etapa do lote, repetida para ler a avaliação sem o lote; a chave estrangeira composta com o lote impede que divirja.");

        builder.Property(a => a.InscricaoId)
            .IsRequired()
            .HasComment("A inscrição avaliada, guardada por valor: quem não tem linha no arquivo do lote também tem avaliação, sem nota.");

        builder.Property(a => a.OfertaCursoId)
            .IsRequired()
            .HasComment("A oferta de curso concorrida (origem da oferta no edital divulgado), guardada por valor e sem chave estrangeira: pertence à configuração do processo.");

        builder.Property(a => a.GrupoAreaEnemCodigo)
            .HasMaxLength(GrupoAreaEnemSnapshot.CodigoMaxLength)
            .IsRequired()
            .HasComment("O código do grupo de área do ENEM cujos pesos apuraram a nota.");

        builder.Property(a => a.Situacao)
            .HasMaxLength(18)
            .IsRequired()
            .HasComment("Situação da avaliação.");

        builder.Property(a => a.Nota)
            .HasColumnType("numeric")
            .HasComment("Σ(nota × peso) ÷ Σ(pesos) das cinco áreas, sem arredondar. Nulo quando não há nota. Não vai para log.");

        builder.HasIndex(a => new { a.LoteId, a.InscricaoId, a.OfertaCursoId })
            .IsUnique()
            .HasDatabaseName("ux_avaliacoes_da_etapa_importacao_inscricao_oferta");

        builder.HasIndex(a => new { a.InscricaoId, a.EtapaProcessoId })
            .HasDatabaseName("ix_avaliacoes_da_etapa_inscricao_etapa");
    }

}
