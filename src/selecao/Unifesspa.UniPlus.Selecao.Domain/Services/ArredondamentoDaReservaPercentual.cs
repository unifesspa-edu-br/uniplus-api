namespace Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// O arredondamento de uma fatia percentual de reserva: por teto, que garante a fração inteira
/// de vaga, ou por piso, que a Lei 12.711 (art. 11) usa para a reserva quilombola sem recorte de renda.
/// </summary>
internal static class ArredondamentoDaReservaPercentual
{
    internal static int PorTeto(int vagasDaFaixa, decimal percentual) =>
        (int)Math.Ceiling(vagasDaFaixa * percentual / 100m);

    internal static int PorPiso(int vagasDaFaixa, decimal percentual) =>
        (int)Math.Floor(vagasDaFaixa * percentual / 100m);
}
