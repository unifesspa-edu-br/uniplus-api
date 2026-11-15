namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// O limite da espera pela trava da linha do processo: sem limite, a operação que chega à trava retida espera o tempo do
/// driver e falha sem explicação.
/// </summary>
public sealed class TravaDoProcessoOptions
{
    public const string SectionName = "TravaDoProcesso";

    /// <summary>O quanto uma operação espera pela trava retida por outra antes de ser recusada.</summary>
    public TimeSpan EsperaMaxima { get; init; } = TimeSpan.FromSeconds(2);

    internal long EsperaMaximaEmMilissegundos => (long)Math.Ceiling(EsperaMaxima.TotalMilliseconds);

    // O banco aceita o limite em milissegundos inteiros de 32 bits.
    internal static bool EhValida(TravaDoProcessoOptions opcoes) =>
        opcoes.EsperaMaxima > TimeSpan.Zero
        && opcoes.EsperaMaxima <= TimeSpan.FromMilliseconds(int.MaxValue);
}
