namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Recusas dos anexos de um ato normativo. Cada anexo é identificado pelo nome dentro do
/// ato e provado pelo hash do arquivo; o que não permite identificar ou provar é recusado.
/// </summary>
public static class AnexoDoAtoErrorCodes
{
    /// <summary>O hash do anexo não é um SHA-256 em hexadecimal minúsculo.</summary>
    public const string HashInvalido = "AnexoDoAto.HashInvalido";

    /// <summary>O nome do anexo está vazio, ou excede o tamanho máximo.</summary>
    public const string NomeInvalido = "AnexoDoAto.NomeInvalido";

    /// <summary>
    /// Dois anexos do mesmo ato têm o mesmo nome. O nome é o que distingue um anexo do
    /// outro para quem consulta o ato.
    /// </summary>
    public const string NomeDuplicado = "AnexoDoAto.NomeDuplicado";

    /// <summary>O ato excede o número máximo de anexos.</summary>
    public const string QuantidadeExcedida = "AnexoDoAto.QuantidadeExcedida";

    /// <summary>
    /// A lista traz um elemento nulo. Descartá-lo mudaria a quantidade e a ordem dos anexos
    /// sem que ninguém soubesse, então a lista inteira é recusada.
    /// </summary>
    public const string Nulo = "AnexoDoAto.Nulo";

    /// <summary>O tipo de mídia do anexo está vazio ou não tem a forma <c>tipo/subtipo</c>.</summary>
    public const string TipoMidiaInvalido = "AnexoDoAto.TipoMidiaInvalido";
}
