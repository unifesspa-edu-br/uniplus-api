namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As vagas que cada sub-reserva federal recebe depois de a escassez cortar o que não cabe nas vagas
/// ofertadas, e quantas vagas esse corte deixou de conceder.
/// </summary>
/// <param name="GruposGarantidos">As sete sub-reservas com garantia mínima, na ordem legal.</param>
/// <param name="QuilombolaSemRecorteDeRenda">A sub-reserva que o art. 11 exclui da garantia.</param>
/// <param name="VagasCortadasPorEscassez">A demanda que as vagas ofertadas não comportaram; zero sem escassez.</param>
internal sealed record AlocacaoDasSubReservasFederais(
    IReadOnlyList<int> GruposGarantidos,
    int QuilombolaSemRecorteDeRenda,
    int VagasCortadasPorEscassez)
{
    private static readonly string[] OrdemDosGruposGarantidos =
    [
        ModalidadesFederaisLei12711.LbPpi,
        ModalidadesFederaisLei12711.LbQ,
        ModalidadesFederaisLei12711.LbPcd,
        ModalidadesFederaisLei12711.LbEp,
        ModalidadesFederaisLei12711.LiPpi,
        ModalidadesFederaisLei12711.LiPcd,
        ModalidadesFederaisLei12711.LiEp,
    ];

    /// <summary>Todas as vagas que as oito sub-reservas receberam.</summary>
    internal int Total => GruposGarantidos.Sum() + QuilombolaSemRecorteDeRenda;

    /// <summary>As oito sub-reservas por código de modalidade, na ordem legal e a quilombola por último.</summary>
    internal Dictionary<string, int> PorModalidade()
    {
        Dictionary<string, int> quadro = new(StringComparer.Ordinal);
        for (int i = 0; i < OrdemDosGruposGarantidos.Length; i++)
        {
            quadro[OrdemDosGruposGarantidos[i]] = GruposGarantidos[i];
        }

        quadro[ModalidadesFederaisLei12711.LiQ] = QuilombolaSemRecorteDeRenda;
        return quadro;
    }
}
