namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// O quadro que o edital fixa, sem cálculo por percentual. Quando a soma fixada passou das vagas
/// ofertadas, o ajuste retirou o excesso, e as vagas retiradas dizem de onde.
/// </summary>
public sealed record QuadroDeVagasFixadoNoEdital(
    IReadOnlyList<VagasDaModalidade> Vagas,
    int TotalDeVagasDoEdital,
    IReadOnlyList<VagasDaModalidade> VagasRetiradasNoAjuste)
    : QuadroDeVagas(Vagas, TotalDeVagasDoEdital)
{
    /// <summary>As vagas retiradas no ajuste na ordem ordinal do código da modalidade.</summary>
    public IReadOnlyList<VagasDaModalidade> VagasRetiradasPeloCodigoDaModalidade() => PeloCodigoDaModalidade(VagasRetiradasNoAjuste);

    public int TotalDeVagasRetiradasNoAjuste => VagasRetiradasNoAjuste.Sum(static vagas => vagas.Quantidade);
}
