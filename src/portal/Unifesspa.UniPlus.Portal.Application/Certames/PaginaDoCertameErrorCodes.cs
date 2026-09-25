namespace Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// Códigos de erro da composição da página pública do certame.
/// </summary>
public static class PaginaDoCertameErrorCodes
{
    /// <summary>
    /// Seleção respondeu que não há certame divulgado com o identificador pedido. A Portal repassa
    /// a resposta sem reaplicar o critério de visibilidade, que vive na origem (ADR-0131).
    /// </summary>
    public const string CertameNaoEncontrado = "PaginaDoCertame.CertameNaoEncontrado";

    /// <summary>
    /// Seleção não respondeu. A Portal não serve conteúdo vencido no lugar: a configuração
    /// publicada tem efeito jurídico, e uma retificação não pode ser encoberta (ADR-0131).
    /// </summary>
    public const string OrigemIndisponivel = "PaginaDoCertame.OrigemIndisponivel";
}
