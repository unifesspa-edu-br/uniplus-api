namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirAlgoritmoContagemPrazo"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// Só o par <c>(codigo, versao)</c> é declarado: o servidor resolve a entrada no rol de
/// regras e congela a identidade completa, com o hash que prova o conteúdo da definição.
/// O hash não entra no corpo porque, ecoado de volta pelo cliente, provaria apenas que ele
/// sabia repeti-lo. Par inteiramente ausente recusa com
/// <c>uniplus.selecao.processo_seletivo.algoritmo_contagem_prazo_nao_declarado</c>; par que
/// não resolve numa entrada de algoritmo de contagem recusa com causa própria.
/// </remarks>
public sealed record DefinirAlgoritmoContagemPrazoRequest(
    string? Codigo,
    string? Versao);
