namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O edital, as etapas, a distribuição de vagas, a classificação e o cronograma lidos do envelope.
/// </summary>
internal sealed record BlocosDaConfiguracao
{
    public required DadosEdital? Dados { get; init; }

    public required string HashDocumento { get; init; }

    public required IReadOnlyList<EtapaProcesso> Etapas { get; init; }

    public required IReadOnlyList<ConfiguracaoDistribuicaoVagas> Distribuicao { get; init; }

    public required OfertaAtendimentoEspecializado? Atendimento { get; init; }

    public required ConfiguracaoBonusRegional? Bonus { get; init; }

    public required ConfiguracaoCascataRemanejamento? Cascata { get; init; }

    public required IReadOnlyList<CriterioDesempate> Desempate { get; init; }

    public required ConfiguracaoClassificacao? Classificacao { get; init; }

    public required IReadOnlyList<FaseCronograma> CronogramaFases { get; init; }
}
