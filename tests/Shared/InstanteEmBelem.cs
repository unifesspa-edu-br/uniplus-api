namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Constrói instantes a partir da zona institucional resolvida, para os testes cujo resultado
/// depende do dia civil (issue #1376).
/// </summary>
/// <remarks>
/// <para>
/// Escrever <c>TimeSpan.FromHours(-3)</c> codifica a premissa que a produção recusa: ela resolve o
/// fuso pela base do runtime, e a base registra 27 períodos de horário de verão em
/// <c>America/Belem</c>, o último encerrado em 06/02/1988. O offset fixo não é frágil só diante de
/// um decreto futuro — dentro daqueles períodos a zona esteve em <c>-02:00</c>, e escrevê-lo já
/// estaria errado. Fora deles a zona sempre esteve em <c>-03:00</c>, inclusive antes de 1988.
/// </para>
/// <para>
/// Mora aqui, e não em <see cref="FusoInstitucional"/>, porque é código que só o teste usa; e não
/// no projeto de suporte existente (<c>Monolito.TestSupport</c>), que arrasta Testcontainers,
/// SSH.NET e o host do monólito para dentro de um projeto de teste de domínio puro. É compilado em
/// cada projeto que precisa dele por <c>&lt;Compile Include&gt;</c> — três cópias do arquivo
/// divergiriam na primeira correção feita em uma só.
/// </para>
/// <para>
/// A assertiva do teste deve comparar contra <b>valor literal</b>, nunca contra outra chamada deste
/// helper: derivar os dois lados pela mesma zona produz um teste que passa mesmo com a produção
/// errada, que é exatamente o risco que a #1376 fecha.
/// </para>
/// </remarks>
internal static class InstanteEmBelem
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId);

    /// <summary>O offset da zona institucional no instante local informado.</summary>
    internal static TimeSpan OffsetEm(DateTime local) => Zona.GetUtcOffset(local);

    /// <exception cref="ArgumentOutOfRangeException">
    /// Quando a hora local não existe na zona (salto de horário de verão) ou ocorre duas vezes
    /// (recuo). Nos dois casos <c>GetUtcOffset</c> devolveria um offset plausível em silêncio, e o
    /// instante resultante não seria a hora pedida — o mesmo modo de falha silencioso que esta
    /// classe existe para eliminar.
    /// </exception>
    internal static DateTimeOffset Em(
        int ano, int mes, int dia, int hora = 0, int minuto = 0, int segundo = 0)
    {
        DateTime local = new(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Unspecified);

        if (Zona.IsInvalidTime(local))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hora),
                local,
                $"{local:yyyy-MM-dd HH:mm:ss} não existe em {FusoInstitucional.ZoneId}: o relógio salta sobre essa hora.");
        }

        if (Zona.IsAmbiguousTime(local))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hora),
                local,
                $"{local:yyyy-MM-dd HH:mm:ss} ocorre duas vezes em {FusoInstitucional.ZoneId}: declare o offset pretendido no teste.");
        }

        return new DateTimeOffset(local, Zona.GetUtcOffset(local));
    }
}
