namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Cryptography;

/// <summary>
/// Nomes de chave usados pelo módulo Discentes em <c>IUniPlusEncryptionService</c>
/// (ADR-0121). Em produção (<c>Provider=vault</c>) a chave correspondente precisa
/// existir no Transit engine antes do deploy — provisionamento é trabalho de
/// infraestrutura (uniplus-infra), fora do escopo deste módulo. Em dev/CI
/// (<c>Provider=local</c>) qualquer nome funciona sem provisionamento prévio.
/// </summary>
public static class DiscentesEncryptionKeys
{
    /// <summary>
    /// Categoria de identificadores pessoais (CPF, RG e afins), isolada por módulo
    /// conforme a ADR-0121. A correlação cross-module acontece via Reader; a chave
    /// criptográfica nunca é compartilhada entre módulos.
    /// </summary>
    public const string IdentificadoresPessoais = "uniplus-discentes-identificadores-aesgcm";
}
