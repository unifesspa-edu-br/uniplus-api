namespace Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Desfecho da verificação de acesso na borda. Distingue o que a borda precisa
/// responder de forma diferente: quem não pôde ser identificado não recebe a
/// mesma resposta de quem foi identificado e não tem a permissão.
/// </summary>
public enum ResultadoDoAcesso
{
    /// <summary>O solicitante pode executar a operação.</summary>
    Permitido = 0,

    /// <summary>Identificado, sem a permissão exigida. A borda responde <c>403</c>.</summary>
    Negado = 1,

    /// <summary>
    /// O token não traz o que identifica o solicitante (emissor, <i>subject</i> ou
    /// <c>jti</c>), então não houve decisão de acesso a tomar. A borda responde
    /// <c>401</c>: dizer <c>403</c> afirmaria uma decisão que não foi tomada.
    /// </summary>
    IdentidadeIncompleta = 2,
}
