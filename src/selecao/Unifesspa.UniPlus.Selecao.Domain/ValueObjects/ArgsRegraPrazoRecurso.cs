namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Args aplicados de <see cref="Entities.RegraRecursoFase"/> — os parâmetros que o
/// admin preenche ao ancorar o prazo de recurso de uma fase na regra
/// <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c> (#854, Story #851 §3.0/§3.6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Os DOIS pares de suspensividade, cada um independente e anulável.</b> A
/// suspensividade não é a mesma nos dois módulos (PO, 14/07): na Seleção as duas
/// instâncias bloqueiam; no Ingresso/Habilitação a 1ª bloqueia e a 2ª (judicial, prazo
/// indeterminado) não. Isso <b>não vira</b> <c>if (modulo == Ingresso)</c> — é
/// configuração: para o Ingresso, <c>SuspensividadeSegundaInstancia*</c> é simplesmente
/// <see langword="null"/>. A ausência <b>é</b> a desativação.
/// </para>
/// <para>
/// Não é uma discriminated union: <see cref="Entities.RegraRecursoFase"/> só admite a
/// regra <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c> (CA-02 recusa qualquer outra), então este
/// VO é a única variante — sem discriminador, sem <c>switch</c>.
/// </para>
/// </remarks>
/// <param name="PrazoValor">Magnitude do prazo de interposição (1ª instância — o Uni+ gere só esta). Em dias úteis, exige valor inteiro.</param>
/// <param name="PrazoUnidade">Unidade do prazo de interposição — só <see cref="UnidadePrazo.DiasUteis"/> e <see cref="UnidadePrazo.Horas"/> são declaráveis; <see cref="Entities.RegraRecursoFase.Criar"/> recusa dia corrido em runtime.</param>
/// <param name="AtoAncoraCodigo">Código do tipo de ato do qual o prazo conta o instante de publicação — sempre o ato produzido pela PRÓPRIA fase.</param>
/// <param name="SuspensividadePrimeiraInstanciaValor">Magnitude da janela de suspensividade da 1ª instância, ou <see langword="null"/> — não bloqueia.</param>
/// <param name="SuspensividadePrimeiraInstanciaUnidade">Unidade da suspensividade da 1ª instância.</param>
/// <param name="SuspensividadeSegundaInstanciaValor">Magnitude da janela de suspensividade da instância superior, ou <see langword="null"/> — não bloqueia (caso normal do Ingresso via judicial).</param>
/// <param name="SuspensividadeSegundaInstanciaUnidade">Unidade da suspensividade da instância superior.</param>
public sealed record ArgsRegraPrazoRecurso(
    decimal PrazoValor,
    UnidadePrazo PrazoUnidade,
    string AtoAncoraCodigo,
    decimal? SuspensividadePrimeiraInstanciaValor,
    UnidadePrazo? SuspensividadePrimeiraInstanciaUnidade,
    decimal? SuspensividadeSegundaInstanciaValor,
    UnidadePrazo? SuspensividadeSegundaInstanciaUnidade);
