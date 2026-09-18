namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Códigos das recusas que acontecem ao <b>ler</b> a requisição, antes de existir comando.
/// São de todo o monólito porque o mecanismo é do framework, e não de nenhum domínio.
/// </summary>
public static class InvalidRequestErrorCodes
{
    /// <summary>O corpo não declara campo que o contrato exige.</summary>
    public const string MissingRequiredField = "Request.MissingRequiredField";

    /// <summary>
    /// O corpo veio e não pôde ser lido — JSON inválido, ou valor que não vira o tipo declarado
    /// <b>dentro do documento</b>. Valor de rota ou query que não converte tem código próprio
    /// (<see cref="InvalidValue"/>): ali há um parâmetro nomeável e o cliente corrige um
    /// endereço, não um documento.
    /// </summary>
    public const string Malformed = "Request.Malformed";

    /// <summary>
    /// Valor de rota ou query presente que não vira o tipo declarado. É distinto de
    /// <see cref="Malformed"/> porque a correção é de outra natureza — muda-se o endereço, não
    /// o corpo — e distinto de uma validação reprovada porque ali o valor chegou a existir no
    /// tipo certo e foi o conteúdo que não serviu.
    /// </summary>
    public const string InvalidValue = "Request.InvalidValue";

    /// <summary>
    /// O recurso exige corpo e a requisição não traz nenhum. É causa distinta de corpo
    /// malformado — ali há documento e ele está errado; aqui não há documento —, e o cliente
    /// corrige de forma diferente, o que é a razão de ter código próprio (ADR-0023).
    /// </summary>
    public const string MissingBody = "Request.MissingBody";
}
