namespace Unifesspa.UniPlus.Geo.Infrastructure.Cep;

/// <summary>
/// Opções do cache-aside do lookup de CEP. CEP é dado estável (muda só com release
/// DNE), então o TTL é longo por padrão. A invalidação fina vem do selo de versão
/// vigente (ADR-0092, #674): a chave compõe-se com o selo, e trocá-lo torna as
/// entradas antigas inalcançáveis — o TTL é só a rede de segurança.
/// </summary>
public sealed class GeoCepCacheOptions
{
    public const string SectionName = "Geo:Cep:Cache";

    /// <summary>TTL das entradas <c>geo:cep:v{versao}:{cep}</c> no Redis (default 24h).</summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// TTL da memoização em processo do selo de versão vigente (#703, default 15s). O
    /// selo só muda em re-selagem do ETL (#674), evento raríssimo, então memoizá-lo
    /// reduz o hot path (cache hit) de 2 round-trips ao Redis (selo + entrada) para 1
    /// (só a entrada). Janela de staleness: após uma re-selagem, a versão anterior do
    /// cache pode ser servida por até este TTL. <c>TimeSpan.Zero</c> (ou negativo)
    /// desliga a memoização — o selo é relido a cada request.
    /// </summary>
    public TimeSpan SeloTtl { get; init; } = TimeSpan.FromSeconds(15);
}
