namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class TurnosOfertaTests
{
    [Fact(DisplayName = "A ordem canônica dos turnos é a do dia: matutino, vespertino, noturno")]
    public void OrdenarCanonicamente_TurnosFora_DeOrdem_DevolveNaOrdemDoDia() =>
        TurnosOferta.OrdenarCanonicamente([TurnoOferta.Noturno, TurnoOferta.Matutino, TurnoOferta.Vespertino])
            .Should().Equal(TurnoOferta.Matutino, TurnoOferta.Vespertino, TurnoOferta.Noturno);

    [Fact(DisplayName = "Dois turnos vêm na ordem do dia, qualquer que seja a ordem de entrada")]
    public void OrdenarCanonicamente_DoisTurnos_DevolveNaOrdemDoDia() =>
        TurnosOferta.OrdenarCanonicamente([TurnoOferta.Noturno, TurnoOferta.Vespertino])
            .Should().Equal(TurnoOferta.Vespertino, TurnoOferta.Noturno);

    [Fact(DisplayName = "Lista nula é recusada")]
    public void OrdenarCanonicamente_ListaNula_Lanca()
    {
        Action act = () => TurnosOferta.OrdenarCanonicamente(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
