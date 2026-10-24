namespace Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O desfecho da verificação. O valor padrão não é um veredicto: quem recebe
/// <see cref="NaoDefinido"/> trata como erro, e nunca como arquivo limpo.
/// </summary>
public enum VeredictoDaVerificacao
{
    NaoDefinido = 0,

    /// <summary>Limpo e promovido ao bucket definitivo.</summary>
    Verificado = 1,

    /// <summary>Recusado pela verificação; o <see cref="MotivoDaRecusa"/> diz o porquê.</summary>
    Recusado = 2,

    /// <summary>A versão confirmada não está na quarentena nem no definitivo: o candidato envia de novo.</summary>
    ReenvioNecessario = 3,
}
