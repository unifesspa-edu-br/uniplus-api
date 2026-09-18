namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Códigos das recusas que acontecem ao <b>ler</b> a requisição, antes de existir comando.
/// São de todo o monólito porque o mecanismo é do framework, e não de nenhum domínio.
/// </summary>
public static class InvalidRequestErrorCodes
{
    /// <summary>O corpo não declara campo que o contrato exige.</summary>
    public const string MissingRequiredField = "Request.MissingRequiredField";

    /// <summary>O corpo não pôde ser lido: JSON inválido, ou valor que não vira o tipo declarado.</summary>
    public const string Malformed = "Request.Malformed";
}
