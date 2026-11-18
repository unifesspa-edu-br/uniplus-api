namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico das regras de <c>tipo=regra_consolidacao</c> do <c>regras_do_catalogo</c>: como as
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

    /// <summary>
    /// O conceito categórico da etapa é o valor que todos os corretores atribuem; o que fazer quando eles
    /// divergem é declarado pela versão da regra.
    /// </summary>
    public const string ConceitoUnanimidade = "CONSOLIDACAO-CONCEITO-UNANIMIDADE";

    /// <summary>
    /// O conceito categórico da etapa é o valor com mais atribuições que cada um dos outros; o tratamento do
    /// empate é declarado pela versão da regra.
    /// </summary>
    public const string ConceitoMaioriaSimples = "CONSOLIDACAO-CONCEITO-MAIORIA-SIMPLES";

    /// <summary>
    /// O conceito categórico da etapa é o valor com mais da metade das atribuições; o tratamento da falta de
    /// maioria é declarado pela versão da regra.
    /// </summary>
    public const string ConceitoMaioriaAbsoluta = "CONSOLIDACAO-CONCEITO-MAIORIA-ABSOLUTA";

    /// <summary>As regras de consolidação semeadas.</summary>
    public static IReadOnlyList<string> Rol { get; } =
        [AvaliacaoUnica, CorrecaoIndependente, ConceitoUnanimidade, ConceitoMaioriaSimples, ConceitoMaioriaAbsoluta];
}
