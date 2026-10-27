namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico das regras de <c>tipo=regra_consolidacao</c> do <c>rol_de_regras</c>: como as
/// notas lançadas pelos avaliadores de uma etapa de avaliação humana viram a nota da etapa.
/// </summary>
/// <remarks>
/// Símbolo, não literal solto. O decodificador do envelope confere o código da referência contra
/// <see cref="Rol"/>, então uma regra nova do tipo entra aqui junto com o seed.
/// </remarks>
public static class RegraConsolidacaoCodigo
{
    /// <summary>Um só avaliador por candidato: a nota lançada é a nota da etapa.</summary>
    public const string AvaliacaoUnica = "CONSOLIDACAO-AVALIACAO-UNICA";

    /// <summary>
    /// Mais de um avaliador corrige cada candidato sem conhecer a nota do outro, e o edital
    /// determina como as correções se combinam (dupla correção).
    /// </summary>
    public const string CorrecaoIndependente = "CONSOLIDACAO-CORRECAO-INDEPENDENTE";

    /// <summary>As regras de consolidação semeadas.</summary>
    public static IReadOnlyList<string> Rol { get; } = [AvaliacaoUnica, CorrecaoIndependente];
}
