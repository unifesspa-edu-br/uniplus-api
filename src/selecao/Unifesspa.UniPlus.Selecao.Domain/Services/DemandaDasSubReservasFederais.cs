namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que cada sub-reserva federal pede antes de a escassez agir: a fatia dos percentuais demográficos,
/// elevada a uma vaga em todo grupo garantido (art. 10 §2º). A quilombola sem recorte de renda não tem
/// garantia e pede só o que o piso do percentual dá.
/// </summary>
/// <param name="GruposGarantidos">A demanda dos sete grupos com garantia mínima, na ordem legal.</param>
/// <param name="QuilombolaSemRecorteDeRenda">A demanda da sub-reserva excluída da garantia pelo art. 11.</param>
internal sealed record DemandaDasSubReservasFederais(
    IReadOnlyList<int> GruposGarantidos,
    int QuilombolaSemRecorteDeRenda)
{
    internal int Total => GruposGarantidos.Sum() + QuilombolaSemRecorteDeRenda;

    internal static DemandaDasSubReservasFederais Calcular(
        int reservaLegalNominal, ReferenciaReservaDemograficaSnapshot demografica)
    {
        int rendaInferior = (int)Math.Ceiling(reservaLegalNominal * 0.5m);
        SubReservasDaFaixaDeRenda baixa = SubReservasDaFaixaDeRenda.DaRendaInferior(rendaInferior, demografica);
        SubReservasDaFaixaDeRenda semRecorte = SubReservasDaFaixaDeRenda.SemRecorteDeRenda(
            reservaLegalNominal - rendaInferior, demografica);

        return new(
            [.. NaOrdemLegal(baixa, semRecorte).Select(static bruto => Math.Max(1, bruto))],
            semRecorte.Quilombolas);
    }

    private static int[] NaOrdemLegal(SubReservasDaFaixaDeRenda baixa, SubReservasDaFaixaDeRenda semRecorte) =>
    [
        baixa.PretosPardosEIndigenas, baixa.Quilombolas, baixa.PessoasComDeficiencia, baixa.EscolaPublica,
        semRecorte.PretosPardosEIndigenas, semRecorte.PessoasComDeficiencia, semRecorte.EscolaPublica,
    ];

    /// <summary>
    /// Sem escassez cada sub-reserva recebe o que pede. Com escassez, duas passadas sobre os grupos
    /// garantidos na ordem legal: primeiro a vaga mínima de cada um enquanto houver espaço, depois o
    /// excedente de cada um; a quilombola sem recorte de renda é servida por último, com o que sobrar.
    /// </summary>
    internal AlocacaoDasSubReservasFederais Alocar(int vagasOfertadasBase) =>
        Total > vagasOfertadasBase
            ? AlocarComEscassez(vagasOfertadasBase)
            : new(GruposGarantidos, QuilombolaSemRecorteDeRenda, VagasCortadasPorEscassez: 0);

    private AlocacaoDasSubReservasFederais AlocarComEscassez(int vagasOfertadasBase)
    {
        int[] alocado = new int[GruposGarantidos.Count];
        int espaco = vagasOfertadasBase - ConcederAVagaMinima(alocado, vagasOfertadasBase);
        espaco -= ConcederOExcedente(alocado, espaco);
        int quilombola = Math.Min(QuilombolaSemRecorteDeRenda, espaco);

        return new(alocado, quilombola, Total - alocado.Sum() - quilombola);
    }

    private static int ConcederAVagaMinima(int[] alocado, int espaco)
    {
        int concedidas = Math.Clamp(espaco, 0, alocado.Length);
        Array.Fill(alocado, 1, 0, concedidas);
        return concedidas;
    }

    private int ConcederOExcedente(int[] alocado, int espaco)
    {
        int restante = espaco;
        for (int i = 0; i < alocado.Length && restante > 0; i++)
        {
            int concedido = Math.Min(GruposGarantidos[i] - alocado[i], restante);
            alocado[i] += concedido;
            restante -= concedido;
        }

        return espaco - restante;
    }
}
