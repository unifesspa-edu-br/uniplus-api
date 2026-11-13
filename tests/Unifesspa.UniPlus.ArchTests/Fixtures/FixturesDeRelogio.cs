namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using Unifesspa.UniPlus.Kernel.Tempo;

/// <summary>
/// Tipos mínimos que servem de contraprova à regra do relógio: cada forma de ler o relógio estático, ou de pegar o do
/// sistema por fora da composição, tem de ser rejeitada com o nome do tipo de fora; o relógio injetado, o instante sem
/// relógio e a exceção permitida têm de passar.
/// </summary>
internal static class FixturesDeRelogio
{
    internal sealed class MetodoComum
    {
        public static DateTime UtcNowDoDateTime() => DateTime.UtcNow;

        public static DateTime NowDoDateTime() => DateTime.Now;

        public static DateTime TodayDoDateTime() => DateTime.Today;

        public static DateTimeOffset UtcNowDoDateTimeOffset() => DateTimeOffset.UtcNow;

        public static DateTimeOffset NowDoDateTimeOffset() => DateTimeOffset.Now;

        public static TimeProvider SystemDoTimeProvider() => TimeProvider.System;
    }

    internal sealed class Lambda
    {
        public static Func<DateTimeOffset> Leitura() => () => DateTimeOffset.UtcNow;
    }

    internal sealed class Closure
    {
        public static Func<DateTimeOffset> Leitura(int dias) => () => DateTimeOffset.UtcNow.AddDays(dias);
    }

    internal sealed class FuncaoLocal
    {
        public static DateTimeOffset Leitura() => Ler();

        private static DateTimeOffset Ler() => DateTimeOffset.UtcNow;
    }

    internal sealed class FuncaoLocalComCaptura
    {
        public static DateTimeOffset Leitura(int dias)
        {
            return Ler();

            DateTimeOffset Ler() => DateTimeOffset.UtcNow.AddDays(dias);
        }
    }

    internal sealed class MetodoAsync
    {
        public static async Task<DateTimeOffset> LeituraAsync()
        {
            await Task.Yield();
            return DateTimeOffset.UtcNow;
        }
    }

    internal sealed class LambdaDentroDeAsync
    {
        public static async Task<Func<DateTimeOffset>> LeituraAsync()
        {
            await Task.Yield();
            return () => DateTimeOffset.UtcNow;
        }
    }

    internal sealed class Iterador
    {
        public static IEnumerable<DateTimeOffset> Leituras()
        {
            yield return DateTimeOffset.UtcNow;
        }
    }

    internal sealed class ConstrutorEstatico
    {
        private static readonly DateTimeOffset Instante;

        static ConstrutorEstatico() => Instante = DateTimeOffset.UtcNow;

        public static DateTimeOffset Leitura => Instante;
    }

    internal sealed class InicializadorDeCampoEstatico
    {
        public static readonly DateTimeOffset Instante = DateTimeOffset.UtcNow;
    }

    internal sealed class InicializadorDeCampoDeInstancia
    {
        public readonly DateTimeOffset Instante = DateTimeOffset.UtcNow;
    }

    internal sealed class InicializadorDePropriedade
    {
        public DateTimeOffset Instante { get; } = DateTimeOffset.UtcNow;
    }

    internal sealed class PropriedadeComExpressao
    {
        public static DateTimeOffset Instante => DateTimeOffset.UtcNow;
    }

    internal sealed class LeOrelogioDoSistemaForaDaComposicao
    {
        public static TimeProvider Relogio() => RelogioEmMicrossegundos.DoSistema;
    }

    internal sealed class UsaORelogioInjetado(TimeProvider relogio)
    {
        public DateTimeOffset Leitura() => relogio.GetUtcNow();
    }

    internal sealed class UsaDateTimeSemRelogio
    {
        public static DateTime Natal() => new(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc);
    }

    internal sealed class ExcecaoPermitida
    {
        public static TimeProvider Fonte() => TimeProvider.System;

        public static TimeProvider Composicao() => RelogioEmMicrossegundos.DoSistema;
    }
}
