namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O resultado da consolidação das atribuições dos corretores num conceito categórico. Só identifica o
/// estado: o que fazer no empate ou sem unanimidade é argumento da regra de consolidação escolhida pelo
/// edital.
/// </summary>
public sealed record ResultadoDaConsolidacaoDoConceito
{
    private ResultadoDaConsolidacaoDoConceito(SituacaoDaConsolidacaoDoConceito situacao, string? valor)
    {
        Situacao = situacao;
        Valor = valor;
    }

    public static ResultadoDaConsolidacaoDoConceito AguardandoCorrecao { get; } = new(SituacaoDaConsolidacaoDoConceito.AguardandoCorrecao, null);

    public static ResultadoDaConsolidacaoDoConceito SemUnanimidade { get; } = new(SituacaoDaConsolidacaoDoConceito.SemUnanimidade, null);

    public static ResultadoDaConsolidacaoDoConceito Empate { get; } = new(SituacaoDaConsolidacaoDoConceito.Empate, null);

    public SituacaoDaConsolidacaoDoConceito Situacao { get; }

    /// <summary>O código do valor consolidado; nulo em todo estado que não seja <see cref="SituacaoDaConsolidacaoDoConceito.Consolidado"/>.</summary>
    public string? Valor { get; }

    public static ResultadoDaConsolidacaoDoConceito Consolidado(string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);

        return new(SituacaoDaConsolidacaoDoConceito.Consolidado, valor);
    }
}
