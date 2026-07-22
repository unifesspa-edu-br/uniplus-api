namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Resultado da avaliação de um <b>átomo</b> — uma <see cref="ValueObjects.CondicaoDnf"/>
/// isolada — contra os fatos de um candidato (Story #926). Tem quatro estados,
/// um a mais que <see cref="Ternario"/>, porque o átomo é o único ponto onde a
/// inaplicabilidade do fato ainda é distinguível.
/// </summary>
/// <remarks>
/// <para>
/// Os quatro estados existem <b>apenas no átomo</b>. Numa cláusula <c>E</c>,
/// <see cref="NaoAplicavel"/> colapsa como <see cref="Ternario.Falso"/> resolvido, e a
/// cláusula continua ternária; o predicado em forma normal disjuntiva também.
/// Não há motor duplo — só o átomo enxerga o quarto estado.
/// </para>
/// <para>
/// O colapso não perde informação onde ela importa: quem consome o veredicto
/// distingue "não exigido em definitivo" de "pendente" pelo resultado do
/// predicado inteiro — <see cref="Ternario.Falso"/> versus
/// <see cref="Ternario.Indeterminado"/> —, e é o átomo
/// <see cref="NaoAplicavel"/> que garante que o primeiro seja alcançado em vez do
/// segundo.
/// </para>
/// <para>
/// <see cref="Indeterminado"/> é <c>0</c> pelo mesmo motivo de
/// <see cref="Ternario"/>: o zero-default tem de ser o estado fail-closed.
/// </para>
/// </remarks>
public enum EstadoAtomo
{
    /// <summary>O fato citado se aplica, mas não se sabe o seu valor — o átomo fica pendente.</summary>
    Indeterminado = 0,

    /// <summary>O fato está resolvido e satisfaz o operador.</summary>
    Verdadeiro = 1,

    /// <summary>O fato está resolvido e não satisfaz o operador.</summary>
    Falso = 2,

    /// <summary>
    /// O fato citado não se aplica ao candidato. Vale para <b>todo</b> operador,
    /// inclusive os de exclusão: a negação inverte um valor, nunca a
    /// inaplicabilidade. Um átomo <c>CONCORRER_PCD DIFERENTE SIM</c> sobre um fato
    /// inaplicável é inaplicável — não verdadeiro.
    /// </summary>
    NaoAplicavel = 3,
}
