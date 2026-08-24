namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Reader de calendário que devolve o dataset informado — e conta quantas vezes foi lido.
/// </summary>
/// <remarks>
/// A contagem existe porque a leitura única é invariante, não detalhe: a mesma resposta alimenta
/// o gate da raiz e o bloco congelado do envelope. Um handler que lesse duas vezes abriria a
/// janela em que o dataset muda entre validar e congelar — e nenhuma asserção sobre o resultado
/// pegaria isso, porque as duas leituras devolveriam o mesmo valor num teste comum.
/// </remarks>
internal sealed class CalendarioVigenteReaderDeTeste(CalendarioVigenteView? vigente) : ICalendarioVigenteReader
{
    private readonly CalendarioVigenteView? _vigente = vigente;

    /// <summary>Quantas vezes <see cref="ObterVigenteAsync"/> foi chamado nesta instância.</summary>
    public int Leituras { get; private set; }

    /// <summary>Ambiente sem dataset vigente — o estado de um sistema recém-instalado.</summary>
    public static CalendarioVigenteReaderDeTeste SemVigente() => new(null);

    /// <summary>Dataset vigente mínimo, com um feriado nacional que basta para o gate passar.</summary>
    public static CalendarioVigenteReaderDeTeste ComVigente() => new(CalendarioDeReferencia());

    /// <summary>
    /// Dataset de referência: um dia de cada abrangência, para que o congelamento exercite as
    /// quatro formas territoriais em vez de só a mais simples.
    /// </summary>
    public static CalendarioVigenteView CalendarioDeReferencia() => new(
        Guid.Parse("01930000-0000-7000-8000-000000000001"),
        "2026",
        [
            new DiaNaoUtilView(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null, null),
            new DiaNaoUtilView(new DateOnly(2026, 8, 15), "ESTADUAL", null, null, null, "PA"),
            new DiaNaoUtilView(new DateOnly(2026, 4, 5), "MUNICIPAL", "1504208", "Marabá", "PA", null),
            new DiaNaoUtilView(new DateOnly(2026, 10, 28), "INSTITUCIONAL", null, null, null, null),
        ]);

    public Task<CalendarioVigenteView?> ObterVigenteAsync(CancellationToken cancellationToken = default)
    {
        Leituras++;
        return Task.FromResult(_vigente);
    }
}
