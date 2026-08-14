namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirLocalidade"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// O trio é a referência de cidade já canônica no sistema (UNI-REQ-0111): sete dígitos no
/// código IBGE, prefixo coerente com a UF informada e nome não vazio. O
/// <c>CodigoIbge</c> é o único valor normativo — dele se derivam o município e a UF cujos
/// feriados incidem na contagem; <c>Nome</c> e <c>Uf</c> são cache de exibição, e não são
/// conferidos contra o código: se divergirem, o rótulo sai errado, nunca o prazo.
/// Trio inteiramente ausente recusa com <c>uniplus.selecao.processo_seletivo.localidade_ausente</c>;
/// trio presente e incoerente recusa com a causa que o Kernel nomeia para a forma.
/// </remarks>
public sealed record DefinirLocalidadeRequest(
    string? CodigoIbge,
    string? Nome,
    string? Uf);
