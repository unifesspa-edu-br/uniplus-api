namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>O eixo de pontuação do processo: as etapas, os critérios de desempate e a classificação.</summary>
public sealed class AvaliacaoDeclarada
{
    public AvaliacaoDeclarada(IReadOnlyList<EtapaProcesso> etapas, IReadOnlyList<CriterioDesempate> criteriosDesempate, ConfiguracaoClassificacao? classificacao)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(criteriosDesempate);
        Etapas = [.. etapas];
        CriteriosDesempate = [.. criteriosDesempate];
        Classificacao = classificacao;
    }

    public IReadOnlyList<EtapaProcesso> Etapas { get; }

    public IReadOnlyList<CriterioDesempate> CriteriosDesempate { get; }

    public ConfiguracaoClassificacao? Classificacao { get; }

    /// <summary>
    /// Divisor da média da nota final: soma dos pesos das etapas que compõem
    /// a nota (caráter classificatória ou ambas, com peso declarado). Fórmula:
    /// <c>NOTA FINAL = Soma(Etapa × peso) / fator_de_divisão</c>, antes do FADA.
    /// </summary>
    public decimal CalcularDivisorMedia() =>
        Etapas.Where(e => e.ComponeNota).Sum(e => e.Peso!.Value);
}
