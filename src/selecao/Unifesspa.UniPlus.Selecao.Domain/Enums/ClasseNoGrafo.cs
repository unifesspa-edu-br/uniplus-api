namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A classe de um nó do grafo de dependência conjunto (Story #928, §6). Campo e fato são nós
/// <b>distintos</b>: sem essa distinção, uma ordenação topológica poderia pôr o nó-fato antes do
/// campo que o produz — a aresta de produção <c>campo → fato</c> é o que garante a precedência.
/// </summary>
public enum ClasseNoGrafo
{
    /// <summary>O campo do formulário — o produtor do valor de um fato <c>DECLARADO</c>.</summary>
    Campo = 0,

    /// <summary>O fato do vocabulário — declarado (produzido por um campo) ou derivado (por regra).</summary>
    Fato = 1,

    /// <summary>Uma exigência de documento — consumidora de fatos pelo gatilho.</summary>
    Exigencia = 2,
}
