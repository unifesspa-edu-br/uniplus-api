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
    /// Categoria de identificadores pessoais (CPF, RG e afins) — key compartilhável
    /// entre módulos que persistam esse tipo de dado, conforme ADR-0121 (nomes e
    /// políticas de key ainda pendentes de aprovação de Segurança/DPO para produção).
    /// </summary>
    public const string IdentificadoresPessoais = "uniplus-pii-identifiers-aesgcm";
}
