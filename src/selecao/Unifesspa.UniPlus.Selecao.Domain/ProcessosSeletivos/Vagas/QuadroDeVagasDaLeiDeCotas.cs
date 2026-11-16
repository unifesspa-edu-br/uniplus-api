namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// O quadro calculado pelo art. 10 da Lei 12.711/2012 (red. Lei 14.723/2023). A conferência da
/// reserva legal diz o que o cálculo concedeu frente ao que a lei nomina.
/// </summary>
public sealed record QuadroDeVagasDaLeiDeCotas(
    IReadOnlyList<VagasDaModalidade> Vagas,
    int TotalDeVagasDoEdital,
    ConferenciaDaReservaLegal ConferenciaDaReservaLegal)
    : QuadroDeVagas(Vagas, TotalDeVagasDoEdital);
