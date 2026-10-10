namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// Índice cego (HMAC) sobre um valor sensível, para buscar ou garantir unicidade sem decifrar
/// o dado em repouso (ADR-0121). Implementações concretas: Vault transit engine HMAC
/// (produção) e HMAC-SHA256 local com subchave derivada por HKDF (dev/CI).
/// <para>
/// <b>Uso restrito a Infrastructure</b> — mesma regra de <see cref="IUniPlusEncryptionService"/>:
/// injetar só em Infrastructure (repositórios, value converters); Application nunca depende
/// diretamente desta interface.
/// </para>
/// <para>
/// O índice não é cifra: é determinístico (mesmo valor e mesma chave sempre dão o mesmo
/// índice) por construção, para servir de índice de banco. Nunca usar para dado que precise
/// de proteção contra análise de frequência sem a mitigação de um HMAC com chave de alta
/// entropia — é exatamente o que este serviço já garante.
/// </para>
/// </summary>
public interface IUniPlusBlindIndexService
{
    /// <summary>
    /// Computa o índice cego de <paramref name="valor"/> usando a chave <paramref name="keyName"/>.
    /// </summary>
    /// <exception cref="EncryptionFailureException">Qualquer falha na operação.</exception>
    Task<byte[]> ComputarAsync(string keyName, ReadOnlyMemory<byte> valor, CancellationToken cancellationToken = default);
}
