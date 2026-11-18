namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;

/// <summary>
/// Como as chamadas ao SIGAA toleram a instabilidade da origem: tempos limite, retentativas
/// e interrupção por falhas.
/// </summary>
public sealed class ResilienciaDoSigaa
{
    /// <summary>
    /// Tempo limite de uma tentativa isolada.
    /// </summary>
    public TimeSpan TimeoutPorTentativa { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Tempo limite da operação inteira, incluindo as retentativas. Precisa ser
    /// estritamente menor que o tempo limite do próprio cliente HTTP, senão o
    /// cancelamento observado vem do cliente e o diagnóstico aponta a causa errada.
    /// </summary>
    public TimeSpan TimeoutTotal { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Quantas vezes repetir uma requisição que falhou por causa transitória.
    /// </summary>
    public int MaximoDeRetentativas { get; init; } = 3;

    /// <summary>
    /// Espera antes da primeira repetição. As seguintes crescem a partir dela, com
    /// variação aleatória para que várias páginas que falharam juntas não voltem todas no
    /// mesmo instante.
    /// </summary>
    public TimeSpan EsperaBaseEntreTentativas { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Quantas chamadas precisam ocorrer na janela de amostragem antes de a interrupção por
    /// falhas considerar abrir. O padrão da biblioteca pressupõe tráfego de API pública
    /// e nunca seria alcançado por uma sincronização diária de poucas dezenas de páginas.
    /// </summary>
    public int ChamadasMinimasParaAvaliarInterrupcao { get; init; } = 10;

    /// <summary>
    /// Janela em que as falhas são contadas para decidir a interrupção por falhas. Precisa ser
    /// pelo menos o dobro do limite de uma tentativa: uma janela que mal comporta uma
    /// tentativa não chega a formar amostra, e a interrupção nunca teria como reagir.
    /// </summary>
    public TimeSpan JanelaDeAmostragemDaInterrupcao { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Proporção de falhas na janela a partir da qual a interrupção por falhas é acionada.
    /// </summary>
    public double ProporcaoDeFalhaParaInterromper { get; init; } = 0.5;

    /// <summary>
    /// Quanto tempo a interrupção por falhas dura antes de deixar passar uma chamada de prova.
    /// </summary>
    public TimeSpan DuracaoDaInterrupcao { get; init; } = TimeSpan.FromSeconds(15);
}
