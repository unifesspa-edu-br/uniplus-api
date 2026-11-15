namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Relógio que o teste avança (ou recua) à vontade, para dar instantes distintos à abertura e à
/// retificação e provar que a vigência segue o relógio do sistema.
/// </summary>
internal sealed class RelogioManualDaVigencia(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _agora = inicio;

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
}
