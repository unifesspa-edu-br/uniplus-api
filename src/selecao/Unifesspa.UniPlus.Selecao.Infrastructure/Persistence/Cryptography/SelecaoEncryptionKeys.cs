namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

/// <summary>
/// Nomes de chave usados pelo módulo Seleção em <c>IUniPlusEncryptionService</c> e
/// <c>IUniPlusBlindIndexService</c> (ADR-0121, emenda de 2026-10-10 — primeiro consumidor real do
/// índice cego: a unicidade de CPF por processo seletivo na inscrição do candidato, issue #1858).
/// Em produção (<c>Provider=vault</c>) as chaves correspondentes precisam existir no Transit engine
/// antes do deploy — provisionamento é trabalho de infraestrutura (uniplus-infra), fora do escopo
/// deste módulo. Em dev/CI (<c>Provider=local</c>) qualquer nome funciona sem provisionamento prévio.
/// </summary>
public static class SelecaoEncryptionKeys
{
    /// <summary>
    /// Categoria dos números de documento do candidato (CPF e os demais fatos <c>Identificador</c>
    /// declarados — RG, documento do estrangeiro), isolada por módulo conforme a ADR-0121. A chave
    /// criptográfica nunca é compartilhada entre módulos.
    /// </summary>
    public const string IdentificadoresAesGcm = "uniplus-selecao-identificadores-aesgcm";

    /// <summary>
    /// Índice cego do CPF: permite localizar a inscrição pelo CPF do token e garantir a unicidade
    /// por processo seletivo (índice único em <c>cpf_indice</c>) sem decifrar a tabela inteira.
    /// </summary>
    public const string IdentificadoresHmac = "uniplus-selecao-identificadores-hmac";
}
