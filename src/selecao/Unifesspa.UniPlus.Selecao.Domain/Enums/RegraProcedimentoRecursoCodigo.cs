namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico das regras de <c>tipo=regra_procedimento_recurso</c> do <c>regras_do_catalogo</c>: como o
/// recurso de uma matéria é revisado.
/// </summary>
/// <remarks>
/// Símbolo, não literal solto. O rol é fechado: o edital escolhe um procedimento da lista, não descreve passos, e
/// acrescentar um procedimento é decisão de produto com versão nova do rol. O decodificador do envelope confere o
/// código da referência contra <see cref="Rol"/>, então uma regra nova do tipo entra aqui junto com o seed.
/// </remarks>
public static class RegraProcedimentoRecursoCodigo
{
    /// <summary>Desempenho avaliado por pessoas: nova correção cega do material.</summary>
    public const string Recorrecao = "PROCEDIMENTO-RECURSO-RECORRECAO";

    /// <summary>Comprovação de um fato e aplicação da pontuação prevista por tabela, sobre o documento e a tabela.</summary>
    public const string Conferencia = "PROCEDIMENTO-RECURSO-CONFERENCIA";

    /// <summary>Erro de registro (transcrição, soma, presença), sempre com motivo da lista da etapa.</summary>
    public const string CorrecaoMaterial = "PROCEDIMENTO-RECURSO-CORRECAO-MATERIAL";

    /// <summary>Cálculo sobre insumos já conferidos (gabarito, média ponderada do ENEM, classificação).</summary>
    public const string Recalculo = "PROCEDIMENTO-RECURSO-RECALCULO";

    /// <summary>Os procedimentos de recurso semeados.</summary>
    public static IReadOnlyList<string> Rol { get; } = [Recorrecao, Conferencia, CorrecaoMaterial, Recalculo];
}
