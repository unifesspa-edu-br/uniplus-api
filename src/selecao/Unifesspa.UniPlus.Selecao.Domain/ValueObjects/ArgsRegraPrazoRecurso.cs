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
/// <param name="PrazoValor">Magnitude do prazo de interposição (1ª instância — o Uni+ gere só esta). Em <see cref="UnidadePrazo.DiasUteis"/>, a borda (validador) exige um número inteiro de dias — sem fração.</param>
/// <param name="PrazoUnidade">Unidade do prazo de interposição — <see cref="UnidadePrazo.DiasUteis"/> exige dataset de calendário vigente e localidade resolvível, conferidos pelo handler antes de chegar aqui (issue #1113).</param>
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
    /// atrasa, o prazo desliza junto, sem retificação. Função pura, testável com dados
    /// fixos — sem I/O; <paramref name="diasNaoUteis"/> chega já resolvido (calendário +
    /// filtro de localidade, issue #1113 CA-04/CA-05) — este método não sabe de
    /// <c>ICalendarioVigenteReader</c> nem de abrangência.
    /// </summary>
    /// <remarks>
    /// Não é executado por esta story (§3.8 — motor de detecção/aplicação da janela é
    /// incremento pós-#40); existe para que o valor congelado seja verificável desde já
    /// (CA-22) e para servir de base ao motor futuro. Sem overload de conveniência: um
    /// default vazio para <paramref name="diasNaoUteis"/> aproximaria em silêncio quando
    /// <see cref="PrazoUnidade"/> for <see cref="UnidadePrazo.DiasUteis"/> — o chamador
    /// sempre decide explicitamente.
    /// </remarks>
    /// <param name="instantePublicacaoAtoAncora">O instante-âncora do qual o prazo conta.</param>
    /// <param name="diasNaoUteis">
    /// Os feriados/recessos que somam aos fins de semana (sempre não úteis) — usado só
    /// quando <see cref="PrazoUnidade"/> é <see cref="UnidadePrazo.DiasUteis"/>. Convenção
    /// de data civil: UTC, a mesma já usada em todo o codebase para vigência de catálogo
    /// (<c>DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)</c>) — um instante
    /// perto da virada do dia em outro fuso pode cair num dia civil diferente do que teria
    /// no fuso local da unidade administradora; não há resolução de fuso por localidade.
    /// </param>
    public DateTimeOffset ResolverFimDaInterposicao(
        DateTimeOffset instantePublicacaoAtoAncora, IReadOnlyCollection<DateOnly> diasNaoUteis)
    {
        ArgumentNullException.ThrowIfNull(diasNaoUteis);
        return PrazoUnidade switch
        {
            UnidadePrazo.Horas => instantePublicacaoAtoAncora.AddHours((double)PrazoValor),
            UnidadePrazo.Dias => instantePublicacaoAtoAncora.AddDays((double)PrazoValor),
            UnidadePrazo.DiasUteis => AdicionarDiasUteis(instantePublicacaoAtoAncora, (int)PrazoValor, diasNaoUteis),
            _ => throw new InvalidOperationException($"Unidade de prazo desconhecida: {PrazoUnidade}."),
        };
    }

    // (int)PrazoValor não trunca fração nem estoura: a borda
    // (DefinirCronogramaFasesCommandValidator) exige PrazoValor inteiro e <= 3650 quando
    // PrazoUnidade é DiasUteis — invariantes de fronteira, não reconferidas aqui (VOs
    // confiam no que já atravessou a borda, ADR-0042).
    private static DateTimeOffset AdicionarDiasUteis(
        DateTimeOffset instante, int quantidadeDiasUteis, IReadOnlyCollection<DateOnly> diasNaoUteis)
    {
        HashSet<DateOnly> naoUteis = [.. diasNaoUteis];
        DateTimeOffset resultado = instante;
        int contados = 0;
        while (contados < quantidadeDiasUteis)
        {
            resultado = resultado.AddDays(1);

            // A data civil é sempre derivada de UtcDateTime — nunca de resultado.DayOfWeek
            // (que reflete o dia LOCAL do offset, não o dia em UTC). Misturar os dois faria
            // o fim de semana e a busca em diasNaoUteis discordarem sobre qual dia civil
            // está sendo avaliado para um instante com offset não-zero perto da virada do
            // dia — a MESMA convenção UTC já usada para "hoje" em
            // DefinirCronogramaFasesCommandHandler.
            DateOnly dataCivil = DateOnly.FromDateTime(resultado.UtcDateTime);
            bool fimDeSemana = dataCivil.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (!fimDeSemana && !naoUteis.Contains(dataCivil))
            {
                contados++;
            }
        }

        return resultado;
    }
}
