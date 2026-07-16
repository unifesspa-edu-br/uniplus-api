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
/// <param name="PrazoValor">Magnitude do prazo de interposição (1ª instância — o Uni+ gere só esta).</param>
/// <param name="PrazoUnidade">Unidade do prazo de interposição — <see cref="UnidadePrazo.DiasUteis"/> é recusado em runtime (sem calendário).</param>
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
    UnidadePrazo? SuspensividadeSegundaInstanciaUnidade)
{
    /// <summary>
    /// Resolve o instante em que o prazo de interposição encerra, a partir do
    /// <b>instante de publicação do ato âncora</b> — nunca de data fixa (CA-22): se o ato
    /// atrasa, o prazo desliza junto, sem retificação. Função pura, testável com
    /// <c>FakeTimeProvider</c> alimentando o instante do ato — sem I/O.
    /// </summary>
    /// <remarks>
    /// Não é executado por esta story (§3.8 — motor de detecção/aplicação da janela é
    /// incremento pós-#40); existe para que o valor congelado seja verificável desde já
    /// (CA-22) e para servir de base ao motor futuro.
    /// </remarks>
    public DateTimeOffset ResolverFimDaInterposicao(DateTimeOffset instantePublicacaoAtoAncora) =>
        PrazoUnidade switch
        {
            UnidadePrazo.Horas => instantePublicacaoAtoAncora.AddHours((double)PrazoValor),
            UnidadePrazo.Dias => instantePublicacaoAtoAncora.AddDays((double)PrazoValor),
            UnidadePrazo.DiasUteis => throw new InvalidOperationException(
                "Prazo em dias úteis não tem calendário para resolver — o gate de publicação recusa este valor antes de chegar aqui."),
            _ => throw new InvalidOperationException($"Unidade de prazo desconhecida: {PrazoUnidade}."),
        };
}
