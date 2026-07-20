namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Veredicto de <see cref="Services.ResolvedorArvoreSatisfacao"/> sobre UM nó da árvore de
/// satisfação (folha ou grupo), para um candidato (Story #920). Compõe a aplicabilidade
/// ternária do gatilho (<see cref="Ternario"/>, change irmã #914) com apresentação/cardinalidade.
/// </summary>
public enum EstadoSatisfacao
{
    Nenhum = 0,

    /// <summary>O nó está coberto — folha com apresentação, ou grupo com filhos suficientes satisfeitos.</summary>
    Satisfeito = 1,

    /// <summary>O nó é aplicável, mas ainda falta apresentação/cardinalidade para satisfazê-lo.</summary>
    Pendente = 2,

    /// <summary>Um fato citado no gatilho de alguma folha do nó não está resolvido para este candidato (fail-closed) — nunca vira Satisfeito só por apresentação.</summary>
    Indeterminado = 3,

    /// <summary>O nó (ou todos os seus filhos) tem gatilho falso — não é exigido deste candidato.</summary>
    NaoAplicavel = 4,

    /// <summary>
    /// Só ocorre em grupo <c>E</c> (propagado de um filho <c>IMPOSSIVEL</c>) ou <c>OU</c>/<c>N-de</c>
    /// (máximo atingível menor que a cardinalidade mínima) — a exigência não pode ser satisfeita
    /// com a config/dados atuais. Sinalizado explicitamente, nunca colapsado para Pendente.
    /// </summary>
    Impossivel = 5,
}
