using Unifesspa.UniPlus.Spikes.EventSourcing.Application;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>Utilitários compartilhados pelas suítes do spike.</summary>
internal static class TestHelpers
{
    /// <summary>Ator fictício para testes — sem PII real (LGPD).</summary>
    public static Ator AtorFicticio() =>
        new(Guid.CreateVersion7(), "Servidor de Teste", "00000000191");

    /// <summary>Aguarda <paramref name="condicao"/> virar verdadeira ou estourar o timeout.</summary>
    public static async Task<bool> EsperarAsync(
        Func<bool> condicao,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < limite)
        {
            if (condicao())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return condicao();
    }
}
