namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Mapeamento EF Core compartilhado do circuito de aprovação da proteção de dados
/// (<see cref="AprovacaoDaProtecao"/>) como owned type obrigatório, em colunas <c>protecao_*</c> da
/// tabela do tipo de documento e do fato. Centralizado aqui para que os dois cadastros guardem o
/// circuito do mesmo jeito. O ciclo e o estado das manifestações técnicas ficam na linha do item para a manifestação
/// e a decisão disputarem o mesmo token de concorrência (ADR-0163).
/// </summary>
internal static class AprovacaoDaProtecaoOwnedConfiguration
{
    private const int SituacaoMaxLength = 20;
    private const int AtorMaxLength = 255;

    private static readonly ValueConverter<SituacaoDaAprovacao, string> SituacaoConverter =
        new(situacao => SituacoesDaAprovacao.ParaTokenCanonico(situacao), token => SituacoesDaAprovacao.Analisar(token));

    public static void Configure<TOwner>(OwnedNavigationBuilder<TOwner, AprovacaoDaProtecao> aprovacao)
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(aprovacao);

        aprovacao.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        aprovacao.Property(a => a.Situacao)
            .HasColumnName("protecao_situacao")
            .HasConversion(SituacaoConverter)
            .HasMaxLength(SituacaoMaxLength)
            .IsRequired();

        aprovacao.Property(a => a.Ciclo)
            .HasColumnName("protecao_ciclo")
            .HasConversion(ManifestacaoDaProtecaoConversores.Ciclo)
            .IsRequired();

        aprovacao.Property(a => a.SolicitadaEm).HasColumnName("protecao_solicitada_em");
        aprovacao.Property(a => a.SolicitadaPor).HasColumnName("protecao_solicitada_por").HasMaxLength(AtorMaxLength)
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);

        aprovacao.Property(a => a.DecididaEm).HasColumnName("protecao_decidida_em");
        aprovacao.Property(a => a.DecididaPor).HasColumnName("protecao_decidida_por").HasMaxLength(AtorMaxLength)
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);

        aprovacao.Property(a => a.ClassificacaoRecomendada)
            .HasColumnName("protecao_recomendada")
            .HasConversion<ClassificacaoDeProtecaoValueConverter>()
            .HasMaxLength(ClassificacaoDeProtecaoValueConverter.MaxLength);

        aprovacao.Property(a => a.Justificativa)
            .HasColumnName("protecao_justificativa")
            .HasMaxLength(Fundamentacao.MaxLength);

        aprovacao.Property(a => a.AceitaEm).HasColumnName("protecao_aceita_em");
        aprovacao.Property(a => a.AceitaPor).HasColumnName("protecao_aceita_por").HasMaxLength(AtorMaxLength)
            .ClassificaComo(ClassificacaoDeProtecao.Pessoal);

        aprovacao.Property(a => a.ManifestadaEm).HasColumnName("protecao_manifestada_em");
        aprovacao.Property(a => a.ManifestacaoDesfavoravelVigente).HasColumnName("protecao_manifestacao_desfavoravel").IsRequired();
        aprovacao.Property(a => a.ManifestacoesNoCiclo).HasColumnName("protecao_manifestacoes_no_ciclo").IsRequired();
    }

    /// <summary>
    /// Os CHECKs do circuito na tabela do dono: a situação pertence ao vocabulário, o item com ajuste recomendado
    /// carrega a classificação recomendada, o reprovado carrega a justificativa, o ciclo começa em 1, a manifestação
    /// desfavorável vigente tem o instante da manifestação, e o instante existe se e só se o ciclo contou manifestação.
    /// </summary>
    public static void ConfigurarChecks<TEntity>(TableBuilder<TEntity> table, string tabela)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(table);

        string tokens = string.Join(", ", SituacoesDaAprovacao.TokensCanonicos.Select(token => $"'{token}'"));
        table.HasCheckConstraint($"ck_{tabela}_protecao_situacao", $"protecao_situacao IN ({tokens})");
        table.HasCheckConstraint(
            $"ck_{tabela}_protecao_ajuste_com_recomendacao",
            "protecao_situacao <> 'AJUSTE_RECOMENDADO' OR protecao_recomendada IS NOT NULL");
        table.HasCheckConstraint(
            $"ck_{tabela}_protecao_reprovada_com_justificativa",
            "protecao_situacao <> 'REPROVADA' OR protecao_justificativa IS NOT NULL");
        table.HasCheckConstraint($"ck_{tabela}_protecao_ciclo_positivo", "protecao_ciclo >= 1");
        table.HasCheckConstraint(
            $"ck_{tabela}_protecao_desfavoravel_manifestada",
            "NOT protecao_manifestacao_desfavoravel OR protecao_manifestada_em IS NOT NULL");
        table.HasCheckConstraint(
            $"ck_{tabela}_protecao_manifestacoes_contadas",
            "protecao_manifestacoes_no_ciclo >= 0 AND (protecao_manifestacoes_no_ciclo = 0) = (protecao_manifestada_em IS NULL)");
    }
}
