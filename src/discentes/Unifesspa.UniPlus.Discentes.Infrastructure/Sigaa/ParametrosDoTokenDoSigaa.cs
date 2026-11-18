namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;

/// <summary>
/// Quanto o token de acesso vale e com que folga ele é renovado.
/// </summary>
public sealed class ParametrosDoTokenDoSigaa
{
    /// <summary>
    /// Validade assumida para o token quando ele não permite descobrir a própria
    /// expiração. É rede de segurança, não o caminho normal.
    /// </summary>
    public TimeSpan ValidadeAssumida { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Limite da validade aceita para um token, qualquer que seja a que ele declare. Protege
    /// contra uma expiração absurda vinda da origem fixar um token morto indefinidamente.
    /// </summary>
    public TimeSpan ValidadeMaxima { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Margem antes da expiração em que o token é renovado, para que uma requisição não
    /// saia com um token que vence no caminho.
    /// </summary>
    public TimeSpan MargemDeRenovacao { get; init; } = TimeSpan.FromMinutes(1);
}
