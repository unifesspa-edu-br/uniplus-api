namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// A resposta da borda a cada desfecho do acesso, num lugar só: o que muda de um atributo para outro é como se recusa
/// (<c>403</c> ou <c>404</c>) e o que se responde ao impedido; permitir e desafiar a identidade incompleta são iguais.
/// </summary>
public static class RespostaDoAcesso
{
    /// <summary>
    /// Traduz o desfecho: permitido segue (<see langword="null"/>), identidade incompleta desafia com <c>401</c>, e
    /// negado e impedido respondem como o atributo recusa.
    /// </summary>
    /// <remarks>
    /// Challenge, e não Unauthorized: o <c>401</c> canônico carrega <c>WWW-Authenticate</c> e o corpo problem+json
    /// escritos pelo <c>OnChallenge</c> do esquema configurado.
    /// </remarks>
    /// <param name="resultado">O desfecho do acesso.</param>
    /// <param name="recusa">A resposta de quem foi identificado e não pode.</param>
    /// <param name="impedido">A resposta do impedido; omitida, é a mesma da <paramref name="recusa"/>.</param>
    public static IActionResult? Para(ResultadoDoAcesso resultado, Func<IActionResult> recusa, Func<IActionResult>? impedido = null)
    {
        ArgumentNullException.ThrowIfNull(recusa);

        return resultado switch
        {
            ResultadoDoAcesso.Permitido => null,
            ResultadoDoAcesso.IdentidadeIncompleta => new ChallengeResult(),
            ResultadoDoAcesso.Impedido when impedido is not null => impedido(),
            _ => recusa(),
        };
    }
}
