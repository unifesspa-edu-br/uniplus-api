namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// O que o cálculo da Lei de Cotas concedeu às reservas frente ao que a lei nomina. A reserva legal
/// nominal é a que o percentual da lei dá sobre as vagas ofertadas; a efetiva é a que o quadro
/// concede, que passa da nominal quando a garantia mínima de uma vaga por reserva a ultrapassa. As
/// vagas cortadas por escassez são as que a oferta não comportou.
/// </summary>
public sealed record ConferenciaDaReservaLegal(int ReservaLegalNominal, int ReservaLegalEfetiva, int VagasCortadasPorEscassez)
{
    /// <summary>O quanto a reserva efetiva passou da nominal; zero quando não passou.</summary>
    public int ExcedenteSobreAReservaLegal => Math.Max(0, ReservaLegalEfetiva - ReservaLegalNominal);
}
