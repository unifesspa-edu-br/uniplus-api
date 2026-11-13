namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

/// <summary>
/// O instante da edição do cronograma é lido uma só vez (ADR-0068): as fases iniciadas e o tipo de ato vigente
/// são consultados no mesmo instante, sem divergir na virada do dia.
/// </summary>
public sealed class DefinirCronogramaFasesRelogioTests
{
    [Fact(DisplayName = "A edição do cronograma lê o relógio uma única vez")]
    public async Task Handle_EdicaoDoCronograma_LeORelogioUmaVez()
    {
        TimeProvider relogio = Substitute.For<TimeProvider>();
        relogio.GetUtcNow().Returns(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        cenario.Relogio = relogio;

        await cenario.DefinirFasesAsync();

        relogio.Received(1).GetUtcNow();
    }
}
