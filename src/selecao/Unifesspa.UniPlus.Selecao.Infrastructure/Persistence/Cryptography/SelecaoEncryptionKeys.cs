namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

/// <summary>
/// Nomes de chave usados pelo módulo Seleção em <c>IUniPlusEncryptionService</c> e
/// <c>IUniPlusBlindIndexService</c> (ADR-0121, emenda de 2026-10-10 — primeiro consumidor real do
/// índice cego: a unicidade de CPF por processo seletivo na inscrição do candidato, issue #1858).
/// Uma chave por categoria de dado (ADR-0121, decisão 3): os números de documento
/// (<c>Identificadores*</c>) e os dados pessoais sensíveis de cifra individual (<c>Sensiveis*</c>) —
/// identidade de gênero e orientação sexual — não compartilham chave.
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

    /// <summary>
    /// Categoria dos dados pessoais sensíveis de cifra individual da inscrição (identidade de gênero e
    /// orientação sexual), na resposta e no comprovante.
    /// </summary>
    public const string SensiveisAesGcm = "uniplus-selecao-sensiveis-aesgcm";

    /// <summary>
    /// HMAC que prende os valores sensíveis ao comprovante a que pertencem (não é índice de busca): a
    /// troca do envelope cifrado entre comprovantes é detectada na leitura.
    /// </summary>
    public const string SensiveisHmac = "uniplus-selecao-sensiveis-hmac";
}
