namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// O campo, o rótulo e os códigos que cada uso dá às recusas de uma <see cref="Fundamentacao"/>: a justificativa da
/// decisão e o texto da manifestação têm a mesma regra e recusas próprias (molde de <see cref="RecusasDaProtecao"/>).
/// </summary>
/// <param name="Campo">O campo do corpo da requisição em que a recusa sai.</param>
/// <param name="Rotulo">Como a mensagem chama o texto ("A justificativa", "O texto da manifestação").</param>
/// <param name="Obrigatoria">Código da recusa do texto obrigatório ausente.</param>
/// <param name="MuitoLonga">Código da recusa do texto acima do limite.</param>
/// <param name="MensagemObrigatoria">A mensagem da recusa do texto obrigatório ausente, que diz quem o exige.</param>
public sealed record RecusasDaFundamentacao(
    string Campo, string Rotulo, string Obrigatoria, string MuitoLonga, string MensagemObrigatoria)
{
    /// <summary>A justificativa da decisão: obrigatória na reprovação.</summary>
    public static RecusasDaFundamentacao DaJustificativa { get; } = new(
        "justificativa",
        "A justificativa",
        ProtecaoDoCatalogoErrorCodes.JustificativaObrigatoria,
        ProtecaoDoCatalogoErrorCodes.JustificativaMuitoLonga,
        "A reprovação exige a justificativa.");

    /// <summary>O texto da manifestação: obrigatório nos pareceres recomenda ajuste e inadequada.</summary>
    public static RecusasDaFundamentacao DoTextoDaManifestacao { get; } = new(
        "texto",
        "O texto da manifestação",
        ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoObrigatorio,
        ProtecaoDoCatalogoErrorCodes.TextoDaManifestacaoMuitoLongo,
        "Os pareceres RECOMENDA_AJUSTE e INADEQUADA exigem o texto.");
}
