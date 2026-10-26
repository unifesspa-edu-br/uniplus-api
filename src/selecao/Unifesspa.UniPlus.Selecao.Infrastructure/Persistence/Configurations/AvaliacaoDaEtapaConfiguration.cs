namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core das avaliações da etapa: uma linha por inscrição e oferta concorrida de cada
/// lote. A nota é <c>numeric</c> sem precisão, de propósito: com escala o banco arredondaria em
/// silêncio, e o arredondamento é regra própria da classificação. Sem cifra, sem nome, sem CPF.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AvaliacaoDaEtapaConfiguration : IEntityTypeConfiguration<AvaliacaoDaEtapa>
{
    public void Configure(EntityTypeBuilder<AvaliacaoDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("avaliacoes_da_etapa", t =>
        {
            t.HasComment("A nota da etapa de uma inscrição numa oferta de curso concorrida (1ª ou 2ª opção), apurada pelos pesos do grupo de área do curso quando o lote foi importado. Append-only; vale com o lote. A nota por área está nos lançamentos do mesmo lote.");
            t.HasCheckConstraint(
                "ck_avaliacoes_da_etapa_situacao",
                $"situacao IN ({(int)SituacaoDaAvaliacao.Apurada}, {(int)SituacaoDaAvaliacao.InscritoSemLinha}, {(int)SituacaoDaAvaliacao.EmConflito})");
            t.HasCheckConstraint(
                "ck_avaliacoes_da_etapa_nota",
                $"(situacao = {(int)SituacaoDaAvaliacao.Apurada}) = (nota IS NOT NULL) AND (nota IS NULL OR nota BETWEEN 0 AND 1000)");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.LoteId).IsRequired();

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
            .HasConversion<short>()
            .IsRequired()
            .HasComment("1 = apurada, 2 = inscrição sem linha no arquivo, 3 = inscrição com linhas em conflito. Só a apurada tem nota; a falta de nota nunca vira zero.");

        builder.Property(a => a.Nota)
            .HasColumnType("numeric")
            .HasComment("Σ(nota × peso) ÷ Σ(pesos) das cinco áreas, sem arredondar. Nulo quando não há nota. Não vai para log.");

        builder.HasIndex(a => new { a.LoteId, a.InscricaoId, a.OfertaCursoId })
            .IsUnique()
            .HasDatabaseName("ux_avaliacoes_da_etapa_lote_inscricao_oferta");

        builder.HasIndex(a => new { a.InscricaoId, a.EtapaProcessoId })
            .HasDatabaseName("ix_avaliacoes_da_etapa_inscricao_etapa");
    }
}
