namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// Um relógio que anda uma hora a cada leitura e conta quantas vezes foi lido: o teste que o dá a um comando prova
/// que o comando lê o relógio uma só vez, porque qualquer segunda leitura devolveria outro instante.
/// </summary>
internal sealed class RelogioDeLeituraContada(DateTimeOffset primeiraLeitura) : TimeProvider
{
    private int _leituras;

    public int Leituras => Volatile.Read(ref _leituras);

    public override DateTimeOffset GetUtcNow() =>
        primeiraLeitura.AddHours(Interlocked.Increment(ref _leituras) - 1);
}
