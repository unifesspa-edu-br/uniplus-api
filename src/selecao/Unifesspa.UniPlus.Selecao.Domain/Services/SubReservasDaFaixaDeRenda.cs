namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As quatro sub-reservas de uma faixa de renda da Lei 12.711: pretos, pardos e indígenas, quilombolas,
/// pessoas com deficiência e o restante, de escola pública. Os percentuais demográficos fatiam a faixa
/// e a escola pública fica com o que sobra, nunca abaixo de zero.
/// </summary>
internal sealed record SubReservasDaFaixaDeRenda(
    int PretosPardosEIndigenas,
    int Quilombolas,
    int PessoasComDeficiencia,
    int EscolaPublica)
{
    /// <summary>A faixa de renda inferior: a fatia quilombola arredonda por teto.</summary>
    internal static SubReservasDaFaixaDeRenda DaRendaInferior(
        int vagasDaFaixa, ReferenciaReservaDemograficaSnapshot demografica) =>
        Compor(vagasDaFaixa, demografica, ArredondamentoDaReservaPercentual.PorTeto(vagasDaFaixa, demografica.QuilombolaPercentual));

    /// <summary>A faixa sem recorte de renda: a fatia quilombola arredonda por piso (art. 11).</summary>
    internal static SubReservasDaFaixaDeRenda SemRecorteDeRenda(
        int vagasDaFaixa, ReferenciaReservaDemograficaSnapshot demografica) =>
        Compor(vagasDaFaixa, demografica, ArredondamentoDaReservaPercentual.PorPiso(vagasDaFaixa, demografica.QuilombolaPercentual));

    private static SubReservasDaFaixaDeRenda Compor(
        int vagasDaFaixa, ReferenciaReservaDemograficaSnapshot demografica, int quilombolas)
    {
        int pretosPardosEIndigenas = ArredondamentoDaReservaPercentual.PorTeto(vagasDaFaixa, demografica.PpiPercentual);
        int pessoasComDeficiencia = ArredondamentoDaReservaPercentual.PorTeto(vagasDaFaixa, demografica.PcdPercentual);
        int escolaPublica = Math.Max(0, vagasDaFaixa - pretosPardosEIndigenas - quilombolas - pessoasComDeficiencia);

        return new(pretosPardosEIndigenas, quilombolas, pessoasComDeficiencia, escolaPublica);
    }
}
