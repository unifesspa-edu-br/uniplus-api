namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As quatro classes de aresta do grafo de dependência conjunto (Story #928, §6). Toda aresta aponta
/// do <b>produtor</b> para o <b>consumidor</b> — quem resolve antes → quem depende —, de modo que uma
/// ordenação topológica válida (grafo acíclico) já é a ordem de coleta.
/// </summary>
/// <remarks>
/// A ordem numérica <c>producao &lt; precondicao &lt; derivacao &lt; gatilho</c> é a ordem canônica de
/// classe de aresta na serialização determinística (Story #928, §7): o mesmo par de nós pode ter
/// arestas de classes diferentes, e sem a classe na chave o desempate empataria. Não é peso de
/// ordenação topológica — é só a chave de canonicalização.
/// </remarks>
public enum TipoArestaGrafo
{
    /// <summary>
    /// <c>campo → fato DECLARADO</c>: o campo do formulário produz o valor do fato. O fato só fica
    /// <c>RESOLVIDO</c>/<c>NAO_APLICAVEL</c> depois que o candidato passa pelo campo.
    /// </summary>
    Producao = 0,

    /// <summary><c>fato → campo/fato</c>: a aplicabilidade condicional (a pré-condição do §2).</summary>
    Precondicao = 1,

    /// <summary><c>fato declarado → fato derivado</c>: a lista de dependências da regra de derivação.</summary>
    Derivacao = 2,

    /// <summary><c>fato → exigência</c>: a exigência é referenciada pelo fato no seu gatilho DNF.</summary>
    Gatilho = 3,
}
